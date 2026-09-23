using Microsoft.JSInterop;

namespace StudySprint.Services;

/// <summary>
/// Saves the current StudySprint login session in browser
/// sessionStorage so authentication survives page reloads.
/// </summary>
public sealed class AuthStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public AuthStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Stores the authenticated user's basic session data.
    /// </summary>
    public async Task SaveSessionAsync(
        string userId,
        string email,
        string accessToken)
    {
        await _jsRuntime.InvokeVoidAsync(
            "sessionStorage.setItem",
            "studysprint_userId",
            userId);

        await _jsRuntime.InvokeVoidAsync(
            "sessionStorage.setItem",
            "studysprint_email",
            email);

        await _jsRuntime.InvokeVoidAsync(
            "sessionStorage.setItem",
            "studysprint_accessToken",
            accessToken);
    }

    /// <summary>
    /// Retrieves a previously saved browser session.
    /// </summary>
    public async Task<StoredSession?> LoadSessionAsync()
    {
        var userId =
            await _jsRuntime.InvokeAsync<string?>(
                "sessionStorage.getItem",
                "studysprint_userId");

        var email =
            await _jsRuntime.InvokeAsync<string?>(
                "sessionStorage.getItem",
                "studysprint_email");

        var accessToken =
            await _jsRuntime.InvokeAsync<string?>(
                "sessionStorage.getItem",
                "studysprint_accessToken");

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        return new StoredSession(
            userId,
            email,
            accessToken);
    }

    /// <summary>
    /// Removes all locally stored authentication data.
    /// </summary>
    public async Task ClearSessionAsync()
    {
        await _jsRuntime.InvokeVoidAsync(
            "sessionStorage.removeItem",
            "studysprint_userId");

        await _jsRuntime.InvokeVoidAsync(
            "sessionStorage.removeItem",
            "studysprint_email");

        await _jsRuntime.InvokeVoidAsync(
            "sessionStorage.removeItem",
            "studysprint_accessToken");
    }
}

public sealed record StoredSession(
    string UserId,
    string Email,
    string AccessToken);