namespace PrCenter.Core.Ports;

/// <summary>
/// What the vault can say about one owner's stored token without decrypting it:
/// the owner and when the token was saved. Carries no token material, so it is
/// safe to read and render while the vault is locked.
/// </summary>
public sealed record OwnerTokenSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerTokenSummary"/> class.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the token belongs to.</param>
    /// <param name="savedAt">
    /// When the token was stored, or <see langword="null"/> for a row written
    /// before the instant was recorded. A null means "not recorded" and is never
    /// substituted with a stand-in value.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null, empty, or whitespace.</exception>
    public OwnerTokenSummary(string owner, DateTimeOffset? savedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        Owner = owner;
        SavedAt = savedAt;
    }

    /// <summary>Gets the GitHub owner (org or account) the token belongs to.</summary>
    public string Owner { get; }

    /// <summary>Gets when the token was stored, or null when the instant was never recorded.</summary>
    public DateTimeOffset? SavedAt { get; }
}
