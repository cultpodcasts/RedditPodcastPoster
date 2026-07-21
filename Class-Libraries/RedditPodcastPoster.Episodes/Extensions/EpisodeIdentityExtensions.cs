using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Extensions;

internal static partial class EpisodeIdentityExtensions
{
    internal static bool HasYouTubeIdentity(this Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube != null;

    internal static bool HasSpotifyIdentity(this Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify != null;

    internal static bool HasAppleIdentity(this Episode episode) =>
        episode.AppleId is > 0 || episode.Urls.Apple != null;

    internal static bool HasYouTubeOrAppleIdentity(this Episode episode) =>
        episode.HasYouTubeIdentity() || episode.HasAppleIdentity();

    internal static string? ResolveSpotifyEpisodeId(string spotifyId, Uri? spotifyUrl)
    {
        if (!string.IsNullOrWhiteSpace(spotifyId))
        {
            return spotifyId;
        }

        if (spotifyUrl == null)
        {
            return null;
        }

        var match = SpotifyEpisodeIdRegex().Match(spotifyUrl.ToString());
        return match.Success ? match.Groups["episodeId"].Value : null;
    }

    internal static bool SpotifyEpisodesMatch(Episode episode, Episode episodeToMerge)
    {
        var existingId = ResolveSpotifyEpisodeId(episode.SpotifyId, episode.Urls.Spotify);
        var incomingId = ResolveSpotifyEpisodeId(episodeToMerge.SpotifyId, episodeToMerge.Urls.Spotify);
        return !string.IsNullOrWhiteSpace(existingId) &&
               !string.IsNullOrWhiteSpace(incomingId) &&
               existingId == incomingId;
    }

    internal static bool IncomingPlatformIdOwnedByAnotherEpisode(
        Episode candidate,
        Episode episodeToMerge,
        IReadOnlyList<Episode> existingEpisodes)
    {
        var incomingSpotifyId = ResolveSpotifyEpisodeId(episodeToMerge.SpotifyId, episodeToMerge.Urls.Spotify);
        if (!string.IsNullOrWhiteSpace(incomingSpotifyId))
        {
            foreach (var existingEpisode in existingEpisodes)
            {
                if (existingEpisode.Id == candidate.Id)
                {
                    continue;
                }

                var existingSpotifyId =
                    ResolveSpotifyEpisodeId(existingEpisode.SpotifyId, existingEpisode.Urls.Spotify);
                if (existingSpotifyId == incomingSpotifyId)
                {
                    return true;
                }
            }
        }

        if (episodeToMerge.AppleId is > 0)
        {
            foreach (var existingEpisode in existingEpisodes)
            {
                if (existingEpisode.Id == candidate.Id)
                {
                    continue;
                }

                if (existingEpisode.AppleId == episodeToMerge.AppleId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [GeneratedRegex(@"episode/(?'episodeId'\w+)")]
    private static partial Regex SpotifyEpisodeIdRegex();
}
