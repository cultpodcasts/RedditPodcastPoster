using CommandLine;

namespace CosmosDbUploader;

public class UploaderRequest
{
    [Option("use-v2", Required = false, Default = false,
        HelpText = "Write to V2 Podcasts, Episodes and supporting containers instead of the legacy CultPodcasts container.")]
    public bool UseV2 { get; set; }
}
