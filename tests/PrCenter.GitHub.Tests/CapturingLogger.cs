using Microsoft.Extensions.Logging;

namespace PrCenter.GitHub.Tests;

/// <summary>
/// A minimal <see cref="ILogger{T}"/> that records the formatted message of
/// every log entry, so tests can assert what was (and was not) logged.
/// </summary>
/// <typeparam name="T">The category type.</typeparam>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => _messages.Add(formatter(state, exception));
}
