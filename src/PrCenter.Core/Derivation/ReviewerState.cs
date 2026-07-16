namespace PrCenter.Core.Derivation;

/// <summary>
/// A reviewer's standing in the roster of a pull request: <see cref="Pending"/>
/// when requested but not yet reviewed, otherwise the verdict of their latest
/// standing review. Distinct from <see cref="Facts.ReviewState"/>, which carries
/// no pending value because a review request is not a review.
/// </summary>
public enum ReviewerState
{
    /// <summary>The reviewer was requested but has not submitted a review.</summary>
    Pending,

    /// <summary>The reviewer approved the pull request.</summary>
    Approved,

    /// <summary>The reviewer requested changes.</summary>
    ChangesRequested,

    /// <summary>The reviewer commented without approving or requesting changes.</summary>
    Commented,
}
