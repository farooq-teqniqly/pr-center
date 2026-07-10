namespace PrCenter.Core.Derivation;

/// <summary>
/// Why a pull request is not shown in the user's review queue. Shown pull
/// requests carry a <see cref="MembershipState"/> instead.
/// </summary>
public enum MembershipExclusion
{
    /// <summary>The pull request is a draft (excluded even when the user is requested).</summary>
    Draft,

    /// <summary>The pull request is closed or merged.</summary>
    ClosedOrMerged,

    /// <summary>The user's latest review approved the pull request and no re-request is pending.</summary>
    Approved,

    /// <summary>The user is not a requested reviewer and has never reviewed the pull request.</summary>
    Untracked,
}
