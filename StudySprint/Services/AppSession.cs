namespace StudySprint.Services;

public sealed class AppSession
{
    public string? UserId { get; private set; }

    public string? Email { get; private set; }

    public string? AccessToken { get; private set; }

    public bool IsLoggedIn =>
        !string.IsNullOrWhiteSpace(UserId) &&
        !string.IsNullOrWhiteSpace(AccessToken);

    public void SignIn(
        string userId,
        string email,
        string accessToken)
    {
        UserId = userId;
        Email = email;
        AccessToken = accessToken;
    }

    public void SignOut()
    {
        UserId = null;
        Email = null;
        AccessToken = null;
    }
}