namespace Discovery;

public sealed class DiscoveryOrchestrationIncompleteException(string message, Exception? innerException = null)
    : Exception(message, innerException);
