namespace Indexer;

public sealed class HourlyOrchestrationIncompleteException(string message) : InvalidOperationException(message);
