using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudySprint.Models;

namespace StudySprint.Services;

/// <summary>
/// Handles authentication and database requests between
/// StudySprint and Supabase.
/// </summary>
public sealed class SupabaseService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _publishableKey;

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public SupabaseService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _supabaseUrl =
            configuration["Supabase:Url"]?.TrimEnd('/')
            ?? throw new InvalidOperationException(
                "Supabase URL is missing.");

        _publishableKey =
            configuration["Supabase:PublishableKey"]
            ?? throw new InvalidOperationException(
                "Supabase publishable key is missing.");
    }

    // -----------------------------------------------------
    // AUTHENTICATION
    // -----------------------------------------------------

    /// <summary>
    /// Authenticates an existing user with email and password.
    /// </summary>
    public async Task<LoginResult> SignInAsync(
        string email,
        string password)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_supabaseUrl}/auth/v1/token?grant_type=password");

            request.Headers.TryAddWithoutValidation(
                "apikey",
                _publishableKey);

            request.Content = JsonContent.Create(
                new
                {
                    email,
                    password
                });

            using var response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return LoginResult.Failed(
                    "Invalid email or password.");
            }

            var authResponse =
                await response.Content
                    .ReadFromJsonAsync<AuthResponse>(
                        JsonOptions);

            if (authResponse is null ||
                authResponse.User is null ||
                string.IsNullOrWhiteSpace(
                    authResponse.User.Id) ||
                string.IsNullOrWhiteSpace(
                    authResponse.AccessToken))
            {
                return LoginResult.Failed(
                    "The login service returned an invalid response.");
            }

            return LoginResult.Succeeded(
                authResponse.User.Id,
                authResponse.User.Email ?? email,
                authResponse.AccessToken);
        }
        catch
        {
            return LoginResult.Failed(
                "StudySprint could not connect to the login service.");
        }
    }

    /// <summary>
    /// Registers a new StudySprint user through Supabase Auth.
    /// The display name is stored as user metadata and is used
    /// by the existing database trigger to create a profile.
    /// </summary>
    public async Task<RegistrationResult> RegisterAsync(
        string displayName,
        string email,
        string password)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_supabaseUrl}/auth/v1/signup");

            request.Headers.TryAddWithoutValidation(
                "apikey",
                _publishableKey);

            request.Content = JsonContent.Create(
                new
                {
                    email,
                    password,
                    data = new
                    {
                        display_name = displayName
                    }
                });

            using var response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var message =
                    await GetErrorMessageAsync(
                        response,
                        "Registration could not be completed.");

                return RegistrationResult.Failed(message);
            }

            var responseContent =
                await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return RegistrationResult.Failed(
                    "The registration service returned an empty response.");
            }

            using var document =
                JsonDocument.Parse(responseContent);

            var root =
                document.RootElement;

            string? userId = null;

            /*
             * Supabase can return:
             *
             * 1. A direct User object when email confirmation is enabled:
             *    {
             *        "id": "...",
             *        "email": "..."
             *    }
             *
             * 2. A session-style response when confirmation is disabled:
             *    {
             *        "access_token": "...",
             *        "user": {
             *            "id": "...",
             *            "email": "..."
             *        }
             *    }
             */

            if (root.TryGetProperty(
                    "user",
                    out var userElement) &&
                userElement.ValueKind ==
                    JsonValueKind.Object)
            {
                if (userElement.TryGetProperty(
                        "id",
                        out var nestedId))
                {
                    userId =
                        nestedId.GetString();
                }
            }
            else if (root.TryGetProperty(
                         "id",
                         out var directId))
            {
                userId =
                    directId.GetString();
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                Console.Error.WriteLine(
                    $"Unexpected signup response: {responseContent}");

                return RegistrationResult.Failed(
                    "The registration service returned an unexpected response.");
            }

            var hasAccessToken =
                root.TryGetProperty(
                    "access_token",
                    out var tokenElement) &&
                tokenElement.ValueKind ==
                    JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(
                    tokenElement.GetString());

            /*
             * No access token normally means email confirmation
             * is required before the user can sign in.
             */
            var requiresConfirmation =
                !hasAccessToken;

            return RegistrationResult.Succeeded(
                requiresConfirmation);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine(
                $"Registration JSON error: {ex}");

            return RegistrationResult.Failed(
                "StudySprint received an invalid response from the registration service.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Registration exception: {ex}");

            return RegistrationResult.Failed(
                $"Registration connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the password of an authenticated user.
    /// </summary>
    public async Task<OperationResult> ChangePasswordAsync(
        string newPassword,
        string accessToken)
    {
        try
        {
            using var request =
                CreateAuthorizedRequest(
                    HttpMethod.Put,
                    $"{_supabaseUrl}/auth/v1/user",
                    accessToken);

            request.Content = JsonContent.Create(
                new
                {
                    password = newPassword
                });

            using var response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var message =
                    await GetErrorMessageAsync(
                        response,
                        "Password could not be changed.");

                return OperationResult.Failed(message);
            }

            return OperationResult.Succeeded();
        }
        catch
        {
            return OperationResult.Failed(
                "StudySprint could not connect to the password service.");
        }
    }

    // -----------------------------------------------------
    // SUBJECT DATABASE METHODS
    // -----------------------------------------------------

    /// <summary>
    /// Returns every subject belonging to the signed-in user.
    /// Row Level Security prevents access to other users' rows.
    /// </summary>
    public async Task<List<SubjectItem>> GetSubjectsAsync(
        string accessToken)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                $"{_supabaseUrl}/rest/v1/subjects" +
                "?select=id,user_id,name,created_at" +
                "&order=created_at.desc",
                accessToken);

        using var response =
            await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "Subjects could not be loaded.");
        }

        var subjects =
            await response.Content
                .ReadFromJsonAsync<List<SubjectItem>>(
                    JsonOptions);

        return subjects ?? new List<SubjectItem>();
    }

    /// <summary>
    /// Searches the user's subjects using a case-insensitive
    /// pattern. StudySprint uses * as a multiple-character
    /// wildcard and ? as a single-character wildcard.
    /// </summary>
    public async Task<List<SubjectItem>> SearchSubjectsAsync(
        string searchText,
        string accessToken)
    {
        var trimmedSearch =
            searchText.Trim();

        if (string.IsNullOrWhiteSpace(trimmedSearch))
        {
            return new List<SubjectItem>();
        }

        // A normal search such as "math" becomes "*math*"
        // so users do not need to enter wildcards manually.
        if (!trimmedSearch.Contains('*') &&
            !trimmedSearch.Contains('?'))
        {
            trimmedSearch =
                $"*{trimmedSearch}*";
        }

        // PostgREST/Supabase uses SQL LIKE-style patterns.
        // % = any number of characters
        // _ = exactly one character
        var databasePattern =
            trimmedSearch
                .Replace("*", "%")
                .Replace("?", "_");

        var encodedPattern =
            Uri.EscapeDataString(
                databasePattern);

        var url =
            $"{_supabaseUrl}/rest/v1/subjects" +
            "?select=id,user_id,name,created_at" +
            $"&name=ilike.{encodedPattern}" +
            "&order=name.asc";

        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                url,
                accessToken);

        using var response =
            await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "Search results could not be loaded.");
        }

        var subjects =
            await response.Content
                .ReadFromJsonAsync<List<SubjectItem>>(
                    JsonOptions);

        return subjects ?? new List<SubjectItem>();
    }

    /// <summary>
    /// Adds a subject belonging to the authenticated user.
    /// </summary>
    public async Task AddSubjectAsync(
        string userId,
        string subjectName,
        string accessToken)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"{_supabaseUrl}/rest/v1/subjects",
                accessToken);

        request.Headers.TryAddWithoutValidation(
            "Prefer",
            "return=minimal");

        request.Content = JsonContent.Create(
            new
            {
                user_id = userId,
                name = subjectName
            });

        using var response =
            await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "The subject could not be saved.");
        }
    }

    /// <summary>
    /// Deletes one subject belonging to the authenticated user.
    /// </summary>
    public async Task DeleteSubjectAsync(
        Guid subjectId,
        string accessToken)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Delete,
                $"{_supabaseUrl}/rest/v1/subjects" +
                $"?id=eq.{subjectId}",
                accessToken);

        using var response =
            await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "The subject could not be deleted.");
        }
    }

    // -----------------------------------------------------
    // REQUEST HELPERS
    // -----------------------------------------------------

    /// <summary>
    /// Creates a request containing the user's authentication
    /// token and the Supabase publishable API key.
    /// </summary>
    private HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string url,
        string accessToken)
    {
        var request =
            new HttpRequestMessage(
                method,
                url);

        request.Headers.TryAddWithoutValidation(
            "apikey",
            _publishableKey);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        return request;
    }

    /// <summary>
    /// Attempts to retrieve a useful Supabase error message.
    /// </summary>
    private static async Task<string>
        GetErrorMessageAsync(
            HttpResponseMessage response,
            string fallback)
    {
        var content =
            await response.Content
                .ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(content))
        {
            return fallback;
        }

        try
        {
            using var document =
                JsonDocument.Parse(content);

            var root =
                document.RootElement;

            foreach (var propertyName in new[]
            {
                "msg",
                "message",
                "error_description",
                "error"
            })
            {
                if (root.TryGetProperty(
                        propertyName,
                        out var property) &&
                    property.ValueKind ==
                        JsonValueKind.String)
                {
                    return property.GetString()
                        ?? fallback;
                }
            }
        }
        catch
        {
            // If Supabase returns a non-JSON response,
            // use the user-friendly fallback instead.
        }

        return fallback;
    }

    // -----------------------------------------------------
    // SUPABASE RESPONSE MODELS
    // -----------------------------------------------------

    private sealed class AuthResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("user")]
        public AuthUser? User { get; set; }
    }

    private sealed class AuthUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}

public sealed record LoginResult(
    bool Success,
    string? UserId,
    string? Email,
    string? AccessToken,
    string? ErrorMessage)
{
    public static LoginResult Succeeded(
        string userId,
        string email,
        string accessToken)
    {
        return new LoginResult(
            true,
            userId,
            email,
            accessToken,
            null);
    }

    public static LoginResult Failed(
        string message)
    {
        return new LoginResult(
            false,
            null,
            null,
            null,
            message);
    }
}

public sealed record RegistrationResult(
    bool Success,
    bool RequiresEmailConfirmation,
    string? ErrorMessage)
{
    public static RegistrationResult Succeeded(
        bool requiresEmailConfirmation)
    {
        return new RegistrationResult(
            true,
            requiresEmailConfirmation,
            null);
    }

    public static RegistrationResult Failed(
        string message)
    {
        return new RegistrationResult(
            false,
            false,
            message);
    }
}

public sealed record OperationResult(
    bool Success,
    string? ErrorMessage)
{
    public static OperationResult Succeeded()
    {
        return new OperationResult(
            true,
            null);
    }

    public static OperationResult Failed(
        string message)
    {
        return new OperationResult(
            false,
            message);
    }
}