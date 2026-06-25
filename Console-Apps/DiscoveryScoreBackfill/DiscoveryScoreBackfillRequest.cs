using CommandLine;

namespace DiscoveryScoreBackfill;

public class DiscoveryScoreBackfillRequest
{
    [Option('a', "all-unprocessed", HelpText = "Score all unprocessed discovery documents.")]
    public bool AllUnprocessed { get; set; }

    [Option('d', "document-ids", Separator = ',', HelpText = "Comma-separated discovery document ids.")]
    public IEnumerable<Guid>? DocumentIds { get; set; }

    [Option("dry-run", HelpText = "Score and analyze without saving to Cosmos.")]
    public bool DryRun { get; set; }

    [Option("evidence-path", HelpText = "Path for markdown evidence report.")]
    public string? EvidencePath { get; set; }
}
