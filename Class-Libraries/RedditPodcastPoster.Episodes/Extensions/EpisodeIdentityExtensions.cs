using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Extensions;

internal static partial class EpisodeIdentityExtensions
{
    internal static bool HasYouTubeIdentity(this Episode episode) =>
        !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)) ||
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.YouTube);

    internal static bool HasSpotifyIdentity(this Episode episode) =>
        !string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)) ||
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.Spotify);

    internal static bool HasAppleIdentity(this Episode episode) =>
        EpisodeServicePresence.AppleEpisodeId(episode) is > 0 ||
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.Apple);

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
        var existingId = ResolveSpotifyEpisodeId(
            EpisodeServicePresence.SpotifyEpisodeId(episode) ?? string.Empty,
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify));
        var incomingId = ResolveSpotifyEpisodeId(
            EpisodeServicePresence.SpotifyEpisodeId(episodeToMerge) ?? string.Empty,
            EpisodeServicePresence.TryGetUrl(episodeToMerge, ServiceKeys.Spotify));
        return !string.IsNullOrWhiteSpace(existingId) &&
               !string.IsNullOrWhiteSpace(incomingId) &&
               existingId == incomingId;
    }

    internal static bool IncomingPlatformIdOwnedByAnotherEpisode(
        Episode candidate,
        Episode episodeToMerge,
        IReadOnlyList<Episode> existingEpisodes)
    {
        var incomingSpotifyId = ResolveSpotifyEpisodeId(
            EpisodeServicePresence.SpotifyEpisodeId(episodeToMerge) ?? string.Empty,
            EpisodeServicePresence.TryGetUrl(episodeToMerge, ServiceKeys.Spotify));
        if (!string.IsNullOrWhiteSpace(incomingSpotifyId))
        {
            foreach (var existingEpisode in existingEpisodes)
            {
                if (existingEpisode.Id == candidate.Id)
                {
                    continue;
                }

                var existingSpotifyId =
                    ResolveSpotifyEpisodeId(
                        EpisodeServicePresence.SpotifyEpisodeId(existingEpisode) ?? string.Empty,
                        EpisodeServicePresence.TryGetUrl(existingEpisode, ServiceKeys.Spotify));
                if (existingSpotifyId == incomingSpotifyId)
                {
                    return true;
                }
            }
        }

        var incomingAppleId = EpisodeServicePresence.AppleEpisodeId(episodeToMerge);
        if (incomingAppleId is > 0)
        {
            foreach (var existingEpisode in existingEpisodes)
            {
                if (existingEpisode.Id == candidate.Id)
                {
                    continue;
                }

                if (EpisodeServicePresence.AppleEpisodeId(existingEpisode) == incomingAppleId)
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
