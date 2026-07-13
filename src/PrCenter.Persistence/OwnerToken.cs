namespace PrCenter.Persistence;

/// <summary>
/// The encrypted fine-grained GitHub personal access token for one owner, keyed
/// by owner. One row per owner; the ciphertext, nonce, and authentication tag
/// are the AES-GCM output under the app-password-derived key.
/// </summary>
internal sealed class OwnerToken
{
    /// <summary>Gets or sets the GitHub owner (org or account) the token belongs to (the primary key).</summary>
    public string Owner { get; set; } = null!;

    /// <summary>Gets or sets the random AES-GCM nonce used to encrypt the token.</summary>
    public byte[] Nonce { get; set; } = null!;

    /// <summary>Gets or sets the AES-GCM ciphertext of the token.</summary>
    public byte[] Ciphertext { get; set; } = null!;

    /// <summary>Gets or sets the AES-GCM authentication tag for the ciphertext.</summary>
    public byte[] Tag { get; set; } = null!;
}
