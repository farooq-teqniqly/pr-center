using Microsoft.Extensions.Logging;

namespace PrCenter.Core.Tests;

/// <summary>
/// A minimal <see cref="ILogger{T}"/> that records the level and formatted
/// message of every log entry, so tests can assert what was (and was not) logged.
/// </summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => _entries.Add((logLevel, formatter(state, exception)));
}
