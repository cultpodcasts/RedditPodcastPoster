using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Search;

public class SpotifyEpisodeTitleSearch(
    ISpotifyClientWrapper spotifyClient,
    ILogger<SpotifyEpisodeTitleSearch> logger) : ISpotifyEpisodeTitleSearch
{
    public const int MaxResults = 10;

    public async Task<IReadOnlyList<SimpleEpisode>> FindCandidates(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        string market)
    {
        if (string.IsNullOrWhiteSpace(request.EpisodeTitle))
        {
            return [];
        }

        var query = string.IsNullOrWhiteSpace(request.PodcastName)
            ? request.EpisodeTitle
            : $"{request.EpisodeTitle} {request.PodcastName.Trim()}";
        var page = await spotifyClient.FindEpisodes(
            new SearchRequest(SearchRequest.Types.Episode, query)
            {
                Market = market,
                Limit = MaxResults
            },
            indexingContext);
        var hits = page?.Items?.Where(x => x != null).Take(MaxResults).ToList() ?? [];
        if (hits.Count == 0)
        {
            return [];
        }

        var hydrated = await spotifyClient.GetSeveral(
            new EpisodesRequest(hits.Select(x => x.Id).ToArray()) { Market = market },
            indexingContext);
        if (hydrated?.Episodes == null)
        {
            logger.LogWarning(
                "Spotify episode title search for '{EpisodeTitle}' returned no hydrated episodes.",
                request.EpisodeTitle);
            return [];
        }

        var allowedIds = hydrated.Episodes
            .Where(x => x != null && BelongsToShow(x, request))
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);
        return hits.Where(x => allowedIds.Contains(x.Id)).ToList();
    }

    private static bool BelongsToShow(FullEpisode episode, FindSpotifyEpisodeRequest request)
    {
        if (episode.Show == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
        {
            return string.Equals(episode.Show.Id, request.PodcastSpotifyId, StringComparison.Ordinal);
        }

        return !string.IsNullOrWhiteSpace(request.PodcastName) &&
               string.Equals(
                   episode.Show.Name?.Trim(),
                   request.PodcastName.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }
}
