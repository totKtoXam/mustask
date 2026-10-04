using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests.Features.Process;

internal sealed record LogRecord(string Category, LogLevel Level, EventId EventId, string Message, Exception? Exception);

/// <summary>
/// Records what is logged, with the event identifier the existing capturing provider
/// of this project drops. It is both an <see cref="ILoggerProvider"/>, for a whole
/// host, and a source of typed loggers, for a class built by hand.
/// </summary>
internal sealed class LogRecorder : ILoggerProvider
{
    public ConcurrentQueue<LogRecord> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, Entries);

    public ILogger<T> For<T>() => new Recorder<T>(Entries);

    /// <summary>The records of one event, in the order they were logged.</summary>
    public IReadOnlyList<LogRecord> Of(EventId eventId) =>
        Entries.Where(entry => entry.EventId.Id == eventId.Id && entry.EventId.Name == eventId.Name).ToList();

    public void Dispose()
    {
    }

    private class Recorder(string category, ConcurrentQueue<LogRecord> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogRecord(category, logLevel, eventId, formatter(state, exception), exception));
    }

    private sealed class Recorder<T>(ConcurrentQueue<LogRecord> entries) : Recorder(typeof(T).FullName ?? typeof(T).Name, entries), ILogger<T>;
}
