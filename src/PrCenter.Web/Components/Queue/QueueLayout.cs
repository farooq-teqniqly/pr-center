using PrCenter.Core.Queue;

namespace PrCenter.Web.Components.Queue;

/// <summary>
/// Pure projection of a flat <see cref="QueueSnapshot"/> into ordered display
/// groups. Groups are ordered by the owner sequence in the snapshot's owner
/// statuses (stable, so repository order within an owner follows first
/// appearance); within a group, items with an update come first, then the most
/// recently updated. Evaluated relative to the user only; no Core state changes.
/// </summary>
internal static class QueueLayout
{
    /// <summary>
    /// Groups and orders the snapshot's items for display.
    /// </summary>
    /// <param name="snapshot">The snapshot whose items to group.</param>
    /// <returns>The ordered display groups.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public static IReadOnlyList<QueueGroupView> Group(QueueSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var ownerOrder = OwnerOrder(snapshot);

        return snapshot
            .Items.GroupBy(item => (item.Identity.Owner, item.Identity.Repository))
            .Select(group => new QueueGroupView(
                group.Key.Owner,
                group.Key.Repository,
                group
                    .OrderByDescending(item => item.HasUpdate)
                    .ThenByDescending(item => item.LastUpdate.At)
                    .ToArray()
            ))
            .OrderBy(group => OwnerRank(ownerOrder, group.Owner))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> OwnerOrder(QueueSnapshot snapshot)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < snapshot.OwnerStatuses.Count; index++)
        {
            order.TryAdd(snapshot.OwnerStatuses[index].Owner, index);
        }

        return order;
    }

    private static int OwnerRank(IReadOnlyDictionary<string, int> order, string owner) =>
        order.TryGetValue(owner, out var rank) ? rank : int.MaxValue;
}
