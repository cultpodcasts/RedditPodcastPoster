using System.Text.RegularExpressions;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public static partial class SpotifyIdResolver
{
    private static readonly Regex SpotifyId = SpotifyEpisodeRegex();

    public static string GetEpisodeId(Uri url)
    {
        return SpotifyId.Match(url.ToString()).Groups["episodeId"].Value;
    }

    [GeneratedRegex(@"episode/(?'episodeId'\w+)")]
    private static partial Regex SpotifyEpisodeRegex();
}