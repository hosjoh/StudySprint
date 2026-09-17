namespace StudySprint.Helpers;

/// <summary>
/// Centralizes StudySprint's client-side password requirements.
/// Supabase also enforces matching rules on the authentication service.
/// </summary>
public static class PasswordRules
{
    public const int MinimumLength = 8;

    /// <summary>
    /// Returns true only when the password meets every StudySprint rule.
    /// </summary>
    public static bool IsValid(string password)
    {
        return GetErrors(password).Count == 0;
    }

    /// <summary>
    /// Returns human-readable messages describing any missing requirements.
    /// </summary>
    public static List<string> GetErrors(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < MinimumLength)
        {
            errors.Add(
                $"Password must contain at least {MinimumLength} characters.");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add(
                "Password must contain at least one uppercase letter.");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add(
                "Password must contain at least one lowercase letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add(
                "Password must contain at least one number.");
        }

        if (!password.Any(character =>
                !char.IsLetterOrDigit(character)))
        {
            errors.Add(
                "Password must contain at least one special character.");
        }

        return errors;
    }
}