namespace PrCenter.Core.Facts;

/// <summary>
/// The verdict a reviewer submitted on a pull request. Pending review requests
/// are not reviews and have no state here; dismissed reviews are out of scope
/// and are not represented.
/// </summary>
public enum ReviewState
{
    /// <summary>The reviewer approved the pull request.</summary>
    Approved,

    /// <summary>The reviewer requested changes.</summary>
    ChangesRequested,

    /// <summary>The reviewer commented without approving or requesting changes.</summary>
    Commented,
}
