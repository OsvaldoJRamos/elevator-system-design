using Microsoft.Extensions.Logging;

namespace ElevatorSystem.Simulator.Tests.Support;

/// <summary>One line that was written to the log.</summary>
/// <param name="Level">How severe it was.</param>
/// <param name="Message">The rendered text.</param>
/// <param name="Exception">The exception attached to it, if any.</param>
internal sealed record LogLine(LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what it was given, so a test can assert on
/// what was logged rather than on the fact that logging did not throw.
/// </summary>
/// <typeparam name="TCategory">The category this logger reports under.</typeparam>
internal sealed class CapturingLogger<TCategory> : ILogger<TCategory>
{
    private readonly List<LogLine> _lines = [];

    public IReadOnlyList<LogLine> Lines => _lines;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _lines.Add(new LogLine(logLevel, formatter(state, exception), exception));
    }
}
