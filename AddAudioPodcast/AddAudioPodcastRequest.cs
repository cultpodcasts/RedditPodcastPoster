using CommandLine;

namespace AddAudioPodcast
{
    public class AddAudioPodcastRequest
    {
        [Value(0, MetaName = "spotify-id", HelpText = "The Spotify-Id of the Podcast to add", Required = true)]
        public string SpotifyId { get; set; }

        [Value(1, MetaName = "episode-title-regex", HelpText = "An optional title regex for occasional-cult-series")]
        public string EpisodeTitleRegex { get; set; }
    }
}
