using CommandLine;

namespace CultPodcasts.DatabasePublisher;

public class PublisherRequest
{
    [Option("use-v2", Required = false, Default = false,
        HelpText = "Read from V2 Podcasts and Episodes containers instead of the legacy CultPodcasts container.")]
    public bool UseV2 { get; set; }
}
