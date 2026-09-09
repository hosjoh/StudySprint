using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudySprint.Models;

namespace StudySprint.Services;

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
                string.IsNullOrWhiteSpace(
                    authResponse.AccessToken) ||
                authResponse.User is null ||
                string.IsNullOrWhiteSpace(
                    authResponse.User.Id))
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

    private HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string url,
        string accessToken)
    {
        var request =
            new HttpRequestMessage(method, url);

        request.Headers.TryAddWithoutValidation(
            "apikey",
            _publishableKey);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        return request;
    }

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
        string errorMessage)
    {
        return new LoginResult(
            false,
            null,
            null,
            null,
            errorMessage);
    }
}