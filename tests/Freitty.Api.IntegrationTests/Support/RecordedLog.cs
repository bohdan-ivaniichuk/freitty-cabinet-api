using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Freitty.Api.IntegrationTests.Support;

// The lines a logger was asked to write, as text.
internal sealed class RecordedLog : ILoggerProvider
{
    private readonly ConcurrentQueue<string> lines = new();

    public IReadOnlyCollection<string> Lines => lines;

    public ILogger CreateLogger(string categoryName)
    {
        return new Recorder(lines);
    }

    public void Dispose()
    {
    }

    private sealed class Recorder(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lines.Enqueue(formatter(state, exception));
        }
    }
}
