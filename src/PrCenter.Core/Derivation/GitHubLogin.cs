namespace PrCenter.Core.Derivation;

/// <summary>
/// Comparison rules for GitHub login names, which are case-insensitive.
/// Centralized so every deriver evaluates "is this the user?" the same way.
/// </summary>
internal static class GitHubLogin
{
    /// <summary>
    /// Determines whether a login belongs to the user the queue is evaluated for.
    /// </summary>
    /// <param name="login">The login to test.</param>
    /// <param name="myLogin">The user's login.</param>
    /// <returns><see langword="true"/> when the logins match case-insensitively.</returns>
    public static bool IsMe(string login, string myLogin) =>
        string.Equals(login, myLogin, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether a login belongs to someone other than the user the
    /// queue is evaluated for.
    /// </summary>
    /// <param name="login">The login to test.</param>
    /// <param name="myLogin">The user's login.</param>
    /// <returns><see langword="true"/> when the logins do not match.</returns>
    public static bool NotMe(string login, string myLogin) => !IsMe(login, myLogin);
}
