using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PrCenter.Persistence;

/// <summary>
/// Value conversion for the diagnostics columns holding a list of strings --
/// contributed pull request identifiers and configured owners -- as one delimited
/// text column rather than a child table. At ring-buffer scale a third table
/// would be over-modeling, and "which polls contained X" is a `LIKE` away.
/// </summary>
/// <remarks>
/// The delimiter is a newline, which cannot occur in a GitHub owner login, a
/// repository name, or the <c>owner/repo#number</c> identifiers built from them.
/// An empty list round-trips as an empty column value, which stays distinct from
/// SQL NULL -- the null-versus-empty distinction is load-bearing for
/// <see cref="PollRun.ConfiguredOwners"/>, where null means the owner
/// enumeration never completed and empty means no owners are configured.
/// </remarks>
internal static class DelimitedStringList
{
    private const char Delimiter = '\n';

    /// <summary>Gets the converter between a string list and its delimited column value.</summary>
    public static ValueConverter<IReadOnlyList<string>, string> Converter { get; } =
        new(list => Join(list), text => Split(text));

    /// <summary>
    /// Gets the converter for a column whose list may be absent, mapping null to
    /// SQL NULL in both directions. Declared separately rather than reusing
    /// <see cref="Converter"/> so the null case is handled in code the reader can
    /// see, instead of relying on where the provider chooses to short-circuit.
    /// </summary>
    // `== null` rather than `is null`: these lambdas become expression trees, which
    // cannot contain a pattern-matching operator.
    public static ValueConverter<IReadOnlyList<string>?, string?> NullableConverter { get; } =
        new(list => list == null ? null : Join(list), text => text == null ? null : Split(text));

    /// <summary>
    /// Gets the comparer EF Core uses to detect changes to a converted list.
    /// Without it, EF compares the lists by reference and misses an edit to an
    /// existing row's contents.
    /// </summary>
    public static ValueComparer<IReadOnlyList<string>> Comparer { get; } =
        new(
            (left, right) => Equal(left, right),
            list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            list => Split(Join(list))
        );

    private static string Join(IReadOnlyList<string> list) => string.Join(Delimiter, list);

    private static IReadOnlyList<string> Split(string text) =>
        text.Length == 0 ? [] : text.Split(Delimiter);

    private static bool Equal(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        left is null
            ? right is null
            : right is not null && left.SequenceEqual(right, StringComparer.Ordinal);
}
