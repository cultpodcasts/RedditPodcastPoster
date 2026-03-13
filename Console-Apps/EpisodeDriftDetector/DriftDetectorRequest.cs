using CommandLine;

namespace EpisodeDriftDetector;

public class DriftDetectorRequest
{
    [Option('c', "correct", Required = false, Default = false,
        HelpText = "Apply corrections: sync drifted podcast metadata fields and enrich missing IDs from service URLs.")]
    public bool Correct { get; set; }
}
