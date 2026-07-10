namespace PrCenter.Core.Derivation;

/// <summary>
/// A shown membership state: the pull request appears in the user's review
/// queue. Hidden pull requests carry a <see cref="MembershipExclusion"/> instead.
/// </summary>
public enum MembershipState
{
    /// <summary>The user is asked to review and has not reviewed yet.</summary>
    AwaitingFirstReview,

    /// <summary>The user's latest review was non-approving, so a re-review is owed.</summary>
    AwaitingReReview,
}
