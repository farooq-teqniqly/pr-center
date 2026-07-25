using PrCenter.Core.Derivation;

namespace PrCenter.Web.Components.Queue;

/// <summary>
/// A presentation grouping of queue items under one owner/repository heading,
/// with its items already ordered for display. Grouping and ordering are a UI
/// concern; the snapshot carries items flat.
/// </summary>
internal sealed record QueueGroupView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueGroupView"/> class.
    /// </summary>
    /// <param name="owner">The GitHub owner the group is under.</param>
    /// <param name="repository">The repository the group is under.</param>
    /// <param name="items">The group's items, already ordered for display.</param>
    public QueueGroupView(string owner, string repository, IReadOnlyList<QueueItem> items)
    {
        Owner = owner;
        Repository = repository;
        Items = items;
    }

    /// <summary>Gets the GitHub owner the group is under.</summary>
    public string Owner { get; }

    /// <summary>Gets the repository the group is under.</summary>
    public string Repository { get; }

    /// <summary>Gets the group's items, already ordered for display.</summary>
    public IReadOnlyList<QueueItem> Items { get; }
}
