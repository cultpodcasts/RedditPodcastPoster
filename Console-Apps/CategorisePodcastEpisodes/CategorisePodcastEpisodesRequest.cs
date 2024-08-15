using CommandLine;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesRequest

{
    [Option('p', "podcast-ids", HelpText = "The Ids of the podcast to categorised, comma-separated")]
    public string? PodcastIds { get; set; }

    [Option('a', "recent", HelpText = "Categorise recently indexed episodes")]
    public bool CategoriseRecent { get; set; }

    [Option('c', "Commit", Default = false, Required = false)]
    public bool Commit { get; set; }

    [Option('r', "Reset-Subject", Default = false, Required = false)]
    public bool ResetSubjects { get; set; }
}