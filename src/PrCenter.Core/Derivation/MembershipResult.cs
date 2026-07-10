namespace PrCenter.Core.Derivation;

/// <summary>
/// The outcome of deriving a pull request's membership: either shown with a
/// <see cref="State"/>, or hidden with an <see cref="Exclusion"/>. The factory
/// methods keep the two cases consistent so an invalid combination cannot form.
/// </summary>
public sealed record MembershipResult
{
    private MembershipResult(bool isShown, MembershipState? state, MembershipExclusion? exclusion)
    {
        IsShown = isShown;
        State = state;
        Exclusion = exclusion;
    }

    /// <summary>Gets a value indicating whether the pull request is shown in the queue.</summary>
    public bool IsShown { get; }

    /// <summary>Gets the shown state, or <see langword="null"/> when the pull request is hidden.</summary>
    public MembershipState? State { get; }

    /// <summary>Gets the reason the pull request is hidden, or <see langword="null"/> when it is shown.</summary>
    public MembershipExclusion? Exclusion { get; }

    /// <summary>
    /// Creates a shown result in the given state.
    /// </summary>
    /// <param name="state">The shown membership state.</param>
    /// <returns>A shown <see cref="MembershipResult"/>.</returns>
    public static MembershipResult Shown(MembershipState state) => new(true, state, null);

    /// <summary>
    /// Creates a hidden result with the given exclusion reason.
    /// </summary>
    /// <param name="exclusion">The reason the pull request is hidden.</param>
    /// <returns>A hidden <see cref="MembershipResult"/>.</returns>
    public static MembershipResult Hidden(MembershipExclusion exclusion) =>
        new(false, null, exclusion);
}
