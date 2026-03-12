using CommandLine;

namespace CosmosDbDownloader;

public class DownloaderRequest
{
    [Option("use-v2", Required = false, Default = false,
        HelpText = "Read from V2 Podcasts, Episodes and supporting containers instead of the legacy CultPodcasts container.")]
    public bool UseV2 { get; set; }
}
