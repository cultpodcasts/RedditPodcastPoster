using Indexer.Models;
using Indexer.Activities;

namespace Indexer.Orchestrations;

public sealed class HourlyOrchestrationIncompleteException(string message) : InvalidOperationException(message);
