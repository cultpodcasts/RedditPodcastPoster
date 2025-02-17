using CommandLine;

namespace EnrichPodcastWithImages;

public class Request
{
    [Option('n', "podcast",
        HelpText = "The name, or part-name, of the podcast to enrich with images", Group = "selector")]
    public string PodcastPartialMatch { get; set; } = "";

    [Option('s', "subject",
        HelpText = "Subject to enrich with images", Group = "selector")]
    public string Subject { get; set; } = "";
}