using CommandLine;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastRequest
{

    [Value(0, MetaName = "Podcast Guid", HelpText = "The Id of the Podcast")]
    public Guid PodcastGuid { get; set; }

    [Value(1, MetaName = "YouTube Playlist ID", HelpText = "")]
    public string PlaylistId { get; set; } = "";

}