using CommandLine;

namespace CosmosDbDownloader;

public class CosmosDbDownloaderRequest
{
    [Option("only", Required = false, Separator = ',',
        HelpText =
            "Download only these containers (comma-separated). Default: all. " +
            "Names: podcasts, episodes, lookups, titlecasing, subjects, discovery, pushsubscriptions, people.")]
    public IEnumerable<string>? Only { get; set; }

    [Option("skip", Required = false, Separator = ',',
        HelpText =
            "Skip these containers (comma-separated). Cannot combine with --only. " +
            "Names: podcasts, episodes, lookups, titlecasing, subjects, discovery, pushsubscriptions, people.")]
    public IEnumerable<string>? Skip { get; set; }

    [Option('o', "overwrite", Required = false, Default = false,
        HelpText = "Overwrite existing local JSON files. Without this flag, an existing file is an error.")]
    public bool Overwrite { get; set; }

    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}
