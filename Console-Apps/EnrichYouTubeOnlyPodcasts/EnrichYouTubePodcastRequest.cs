using CommandLine;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastRequest
{

    [Value(0, MetaName = "Podcast Guid", HelpText = "The Id of the Podcast", Required = true)]
    public Guid PodcastGuid { get; set; }

    [Value(1, MetaName = "YouTube Playlist ID", HelpText = "")]
    public string PlaylistId { get; set; } = "";

    [Option('r',"released-since", HelpText = "Only ingest items released within these days" )]
    public int? ReleasedSince { get; set; }

    [Option('a',"acknowledge-expensive-query", HelpText = "Acknowledges Playlist-Query is expensive")]
    public bool AcknowledgeExpensiveYouTubePlaylistQuery { get; set; }
}