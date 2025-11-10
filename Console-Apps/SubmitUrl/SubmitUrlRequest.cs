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

    [Option('a', "acknowledge-expensive-queries", Required = false, Default = false,
        HelpText = "Allow expensive queries")]
    public bool AllowExpensiveQueries { get; set; }

    [Option('m', "match-other-services", Required = false, Default = false, HelpText = "Match other services")]
    public bool MatchOtherServices { get; set; }

    [Option('d', "dry-run", Required = false, Default = false, HelpText = "Do not commit to database")]
    public bool DryRun { get; set; }

    [Option('i', "no-index", Default = false, HelpText = "Do not reindex search-index")]
    public bool NoIndex { get; set; }

    [Option('l', "is-internet-archive-playlist", Default = false, HelpText = "Url contains a playlist of internet-archive urls to submit")]
    public bool IsInternetArchivePlaylist { get; set; }

}