using CommandLine;

namespace CatalogueMigrate;

public class CatalogueMigrateRequest
{
    [Option("kind", Required = true,
        HelpText = "NewsReport, Film, or TvShowEpisode. Run News then Film then TV.")]
    public string Kind { get; set; } = string.Empty;

    [Option("podcast-id", Required = false,
        HelpText = "Move one podcast. Required for Film and TV. Optional for News (default is identify-scan).")]
    public Guid? PodcastId { get; set; }

    [Option("apply", Required = false, Default = false,
        HelpText = "Refused in this Build. GATE 5 is the only apply path.")]
    public bool Apply { get; set; }

    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}
