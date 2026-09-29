using CommandLine;

namespace CatalogueMigrateIdentify;

public class CatalogueMigrateIdentifyRequest
{
    [Option("podcast-id", Required = false, HelpText = "Identify a single podcast by id. Default is every podcast.")]
    public Guid? PodcastId { get; set; }

    [Option("apply", Required = false, Default = false,
        HelpText = "Refused in this Build. GATE 5 is the only apply path.")]
    public bool Apply { get; set; }

    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}
