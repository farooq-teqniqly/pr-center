namespace PrCenter.Core.Queue;

/// <summary>
/// Use case exposing the most recently published review-queue snapshot. Returns
/// an explicit never-polled result (a null snapshot) so a consumer can tell "not
/// polled yet since process start" apart from "polled and found no pull requests".
/// </summary>
public sealed class GetQueue
{
    private readonly QueueSnapshotHolder _holder;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetQueue"/> class.
    /// </summary>
    /// <param name="holder">The holder of the most recently published snapshot.</param>
    public GetQueue(QueueSnapshotHolder holder) => _holder = holder;

    /// <summary>
    /// Reads the current queue snapshot.
    /// </summary>
    /// <returns>
    /// The latest published snapshot, or <see langword="null"/> when no refresh has
    /// published one since process start (the never-polled state).
    /// </returns>
    public QueueSnapshot? Execute() => _holder.Current;
}
