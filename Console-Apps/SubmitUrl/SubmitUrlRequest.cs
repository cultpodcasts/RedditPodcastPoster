using CommandLine;

namespace SubmitUrl;

public class SubmitUrlRequest
{
    [Option('y', "skip-youtube-url-enrichment", Required = false, HelpText = "Skip YouTube-Url resolving",
        Default = false)]
    public bool SkipYouTubeUrlResolving { get; set; }

    [Option('f', "submit-urls-in-file", Required = false, HelpText = "Use urls in provided file",
        Default = false)]
    public bool SubmitUrlsInFile { get; set; }

    [Value(0, MetaName = "url of file", HelpText = "The Url or file containing Urls to submit", Required = true)]
    public string UrlOrFile { get; set; } = "";

    [Option('p', "podcastid", Required = false, HelpText = "The Id of the podcast to add this episode to")]
    public Guid? PodcastId { get; set; }

    [Option('a', "acknowledge-expensive-queries", Required = false, HelpText = "Allow expensive queries")]
    public bool AllowExpensiveQueries { get; set; }

    [Option('m', "match-other-services", Required = false, HelpText = "Match other services")]
    public bool MatchOtherServices { get; set; }

}