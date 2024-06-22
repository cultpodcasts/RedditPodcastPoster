using CommandLine;

namespace Index;

public class IndexRequest
{
    [Option('p', "podcast-id", Required = false, HelpText = "The id of the podcast to index")]
    public Guid? PodcastId { get; set; }

    [Option('r', "released-since", Default = 2, HelpText = "Will index episodes released within this many days")]
    public int ReleasedSince { get; set; }

    [Option('y', "skip-expensive-youtube-queries", Default = false, HelpText = "Skip expensive YouTube queries")]
    public bool SkipExpensiveYouTubeQueries { get; set; }

    [Option('d', "skip-podcast-discovery", Default = true, HelpText = "Skip podcast discovery")]
    public bool SkipPodcastDiscovery { get; set; }

    [Option('s', "skip-expensive-spotify-queries", Default = false, HelpText = "Skip expensive Spotify queries")]
    public bool SkipExpensiveSpotifyQueries { get; set; }

    [Option('t', "skip-youtube-url-resolving", Default = false, HelpText = "Skip YouTube url resolution")]
    public bool SkipYouTubeUrlResolving { get; set; }

    [Option('f', "skip-spotify-url-resolving", Default = false, HelpText = "Skip Spotify url resolution")]
    public bool SkipSpotifyUrlResolving { get; set; }
    
    [Option('i', "skip-spotify-indexing", Default = false, HelpText = "Skip Spotify indexing")]
    public bool SkipSpotifyIndexing { get; set; }

}