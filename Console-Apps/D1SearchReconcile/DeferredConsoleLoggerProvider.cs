using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace D1SearchReconcile;

/// <summary>
/// The console's only logger (#1475). While the Spectre progress display is live (<see cref="Hold"/>), log lines are
/// queued instead of written, because a line written to the console under a live display is drawn over by the next
/// refresh and comes out garbled. Disposing the hold writes the queued lines, in order, to standard error after the
/// display has finished. Outside a hold, lines are written straight away. Level filtering is the usual
/// <c>Logging</c> configuration (appsettings.json), which applies to every provider.
/// </summary>
public sealed class DeferredConsoleLoggerProvider(TextWriter? output = null) : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _held = new();
    private readonly object _gate = new();
    private int _holds;

    private TextWriter Output => output ?? Console.Error;

    /// <summary>Queues log lines until the returned handle is disposed, then writes them.</summary>
    public IDisposable Hold()
    {
        Interlocked.Increment(ref _holds);
        return new Release(this);
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose() => Flush();

    private void Write(string line)
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _holds) > 0)
            {
                _held.Enqueue(line);
                return;
            }

            Output.WriteLine(line);
        }
    }

    private void Flush()
    {
        lock (_gate)
        {
            while (_held.TryDequeue(out var line))
            {
                Output.WriteLine(line);
            }

            Output.Flush();
        }
    }

    internal static string Format(LogLevel level, string category, EventId eventId, string message, Exception? exception)
    {
        var text = $"{Label(level)}: {category}[{eventId.Id}]{Environment.NewLine}      {message}";
        return exception is null ? text : $"{text}{Environment.NewLine}{exception}";
    }

    private static string Label(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => level.ToString()
    };

    private sealed class Release(DeferredConsoleLoggerProvider owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (Interlocked.Decrement(ref owner._holds) == 0)
            {
                owner.Flush();
            }
        }
    }

    private sealed class Logger(DeferredConsoleLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            owner.Write(Format(logLevel, category, eventId, formatter(state, exception), exception));
        }
    }
}
