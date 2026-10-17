using Microsoft.Extensions.Logging;
using XTouch.Contracts;

namespace XTouch.Service.Logging;

/// <summary>Keeps recent log entries for the UI and raises an event for each new one.</summary>
public sealed class UiLogSink
{
    private const int Capacity = 500;
    private readonly Queue<LogEntryDto> _recent = new();
    private readonly Lock _gate = new();

    public event Action<LogEntryDto>? Entry;

    public IReadOnlyList<LogEntryDto> Recent() { lock (_gate) return [.. _recent]; }

    public void Write(LogEntryDto e)
    {
        lock (_gate)
        {
            _recent.Enqueue(e);
            while (_recent.Count > Capacity) _recent.Dequeue();
        }
        Entry?.Invoke(e);
    }
}

public sealed class UiLoggerProvider(UiLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new UiLogger(sink, categoryName);
    public void Dispose() { }

    private sealed class UiLogger(UiLogSink sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information && !category.StartsWith("Microsoft.", StringComparison.Ordinal) ||
            logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            if (exception is not null) message += $" ({exception.GetType().Name}: {exception.Message})";
            int dot = category.LastIndexOf('.');
            sink.Write(new LogEntryDto(DateTimeOffset.Now, logLevel.ToString(), dot >= 0 ? category[(dot + 1)..] : category, message));
        }
    }
}
