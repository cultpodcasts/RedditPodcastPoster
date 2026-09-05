using CommandLine;

namespace SubmitUrl;

public class SubmitUrlRequest
{
    [Option('y', "skip-youtube-url-enrichment", Required = false, HelpText = "Skip YouTube-Url resolving",
        Default = false)]
    public bool SkipYouTubeUrlResolving { get; set; }

    [Option('f', "submit-urls-in-file", Required = false, Default = false, HelpText = "Use urls in provided file")]
    public bool SubmitUrlsInFile { get; set; }

    [Value(0, MetaName = "url or file", HelpText = "The Url or file containing Urls to submit", Required = true)]
    public string UrlOrFile { get; set; } = "";

    [Option('p', "podcastid", Required = false, HelpText = "The Id of the podcast to add this episode to")]
    public Guid? PodcastId { get; set; }

    [Option('n', "podcast-name", Required = false, HelpText = "Name of the series to add this episode to, or to create")]
    public string? PodcastName { get; set; }

    [Option('a', "acknowledge-expensive-queries", Required = false, Default = false,
        HelpText = "Allow expensive queries")]
    public bool AllowExpensiveQueries { get; set; }

    [Option('m', "match-other-services", Required = false, Default = false, HelpText = "Match other services")]
    public bool MatchOtherServices { get; set; }

    [Option('d', "dry-run", Required = false, Default = false, HelpText = "Do not commit to database")]
    public bool DryRun { get; set; }

    [Option('i', "no-index", Default = false, HelpText = "Do not reindex search-index")]
    public bool NoIndex { get; set; }

    [Option('l', "is-internet-archive-playlist", Default = false,
        HelpText = "Url contains a playlist of internet-archive urls to submit")]
    public bool IsInternetArchivePlaylist { get; set; }

    [Option('c', "create-podcast", Default = false, HelpText = "Create new podcast")]
    public bool CreatePodcast { get; set; }

    [Option('r', "refresh-meta", Required = false, Default = false,
        HelpText = "Overwrite title/description/release/length/image on an existing matched episode from freshly extracted meta")]
    public bool RefreshMeta { get; set; }

    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}