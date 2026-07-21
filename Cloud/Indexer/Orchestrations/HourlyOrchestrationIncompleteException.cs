using Indexer.Activities;
using Indexer.Models;

namespace Indexer.Orchestrations;

public sealed class HourlyOrchestrationIncompleteException(string message) : InvalidOperationException(message);
