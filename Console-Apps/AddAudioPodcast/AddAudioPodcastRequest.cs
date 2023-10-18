using CommandLine;

namespace AddAudioPodcast;

public class AddAudioPodcastRequest
{
    [Value(0, MetaName = "podcast-id", HelpText = "The Spotify-Id  or Apple-Id of the Podcast to add", Required = true)]
    public string PodcastId { get; set; } = "";

    [Value(1, MetaName = "episode-title-regex", HelpText = "An optional title regex for occasional-cult-series")]
    public string EpisodeTitleRegex { get; set; } = "";

    [Option('a', "apple-podcast-authority", Required = false,
        HelpText = "Whether to use Apple Podcasts for release authority", Default = false)]
    public bool AppleReleaseAuthority { get; set; }

    [Option('m', "spotify-marker", Default = null, Required = false, HelpText = "The Spotify-Market to search against")]
    public string? SpotifyMarket { get; set; }
}