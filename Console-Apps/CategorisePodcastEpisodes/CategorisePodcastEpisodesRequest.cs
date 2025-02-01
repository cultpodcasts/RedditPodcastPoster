using CommandLine;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesRequest

{
    [Option('p', "podcast-ids", HelpText = "The Ids of the podcast to categorised, comma-separated",
        Group = "Identifier")]
    public string? PodcastIds { get; set; }

    [Option('n', "podcast-partial-match", HelpText = "The name, or part-name, of the podcast to enrich with images",
        Group = "Identifier")]
    public string PodcastPartialMatch { get; set; } = "";

    [Option('a', "recent", HelpText = "Categorise recently indexed episodes")]
    public bool CategoriseRecent { get; set; }

    [Option('c', "Commit", Default = false, Required = false)]
    public bool Commit { get; set; }

    [Option('r', "Reset-Subject", Default = false, Required = false)]
    public bool ResetSubjects { get; set; }
}