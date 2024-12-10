using CommandLine;

namespace EnrichPodcastWithImages;

internal class Request
{
    [Value(0, MetaName = "podcast-partial-match",
        HelpText = "The name, or part-name, of the podcast to enrich with images", Required = true)]
    public string PodcastPartialMatch { get; set; } = "";
}