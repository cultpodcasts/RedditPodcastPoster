using Discovery.Activities;
using Discovery.Models;
using Discovery.Services;

namespace Discovery.Orchestrations;

public sealed class DiscoveryOrchestrationIncompleteException(string message, Exception? innerException = null)
    : Exception(message, innerException);
