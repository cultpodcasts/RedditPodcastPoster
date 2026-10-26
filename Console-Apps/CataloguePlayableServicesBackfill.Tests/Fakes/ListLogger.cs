using Microsoft.Extensions.Logging;

namespace CataloguePlayableServicesBackfill.Tests.Fakes;

/// <summary>
/// Captures log messages at or above a threshold for behavioural assertions.
/// </summary>
internal sealed class ListLogger : ILogger
{
    private readonly LogLevel _minimumLevel;

    public ListLogger(LogLevel minimumLevel = LogLevel.Warning) => _minimumLevel = minimumLevel;

    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        Messages.Add(formatter(state, exception));
    }
}
