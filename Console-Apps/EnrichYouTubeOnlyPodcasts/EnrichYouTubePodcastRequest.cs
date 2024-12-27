using CommandLine;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastRequest
{

    [Option('p', longName:  "podcast-id", HelpText = "The Id of the Podcast")]
    public Guid? PodcastGuid { get; set; }

    [Option('i', longName: "youtube-playlist-id", HelpText = "")]
    public string? PlaylistId { get; set; } = "";

    [Option('n', "podcast-name", Required = false, HelpText = "The name of the podcast to index")]
    public string? PodcastName { get; set; }

    [Option('r',"released-since", HelpText = "Only ingest items released within these days" )]
    public int? ReleasedSince { get; set; }

    [Option('a',"acknowledge-expensive-query", HelpText = "Acknowledges Playlist-Query is expensive")]
    public bool AcknowledgeExpensiveYouTubePlaylistQuery { get; set; }

    [Option('s', "include-shorts", HelpText = "Include Short videos")]
    public bool IncludeShort { get; set; }
}