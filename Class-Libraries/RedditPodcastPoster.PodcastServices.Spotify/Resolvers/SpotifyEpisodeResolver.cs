using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public class SpotifyEpisodeResolver(
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifySearchResultFinder searchResultFinder,
    ILogger<SpotifyEpisodeResolver> logger)
    : ISpotifyEpisodeResolver
{
    public async Task<FindEpisodeResponse> FindEpisode(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        Func<SimpleEpisode, bool>? reducer = null)
    {
        var market = request.Market ?? Market.CountryCode;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                "Skipping '{nameofFindEpisode}' as '{nameofSkipSpotifyUrlResolving}' is set. Podcast-Id:'{requestPodcastSpotifyId}', Podcast-Name:'{requestPodcastName}', Episode-Id:'{requestEpisodeSpotifyId}', Episode-Name:'{requestEpisodeTitle}'.",
                nameof(FindEpisode), nameof(indexingContext.SkipSpotifyUrlResolving), request.PodcastSpotifyId,
                request.PodcastName, request.EpisodeSpotifyId, request.EpisodeTitle);
            return new FindEpisodeResponse(null);
        }

        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            var episodeRequest = new EpisodeRequest { Market = market };
            fullEpisode = await spotifyClientWrapper.GetFullEpisode(request.EpisodeSpotifyId, episodeRequest, indexingContext);
            if (fullEpisode != null)
            {
                return new FindEpisodeResponse(TakeIfFree(fullEpisode));
            }
        }

        var podcastEpisodes = await spotifyPodcastEpisodesProvider.GetAllEpisodes(request, indexingContext, market);

        SimpleEpisode? matchingEpisode;
        if (request.Length is { } episodeLength && episodeLength > TimeSpan.Zero &&
            (request.ReleaseAuthority == Service.YouTube || request.EnrichingYouTubeDiscoveredEpisode))
        {
            matchingEpisode = searchResultFinder.FindMatchingEpisodeByLength(
                request.EpisodeTitle,
                episodeLength,
                podcastEpisodes.Episodes,
                reducer,
                request.ReleaseAuthority,
                request.Released,
                request.EnrichingYouTubeDiscoveredEpisode);
        }
        else
        {
            matchingEpisode =
                searchResultFinder.FindMatchingEpisodeByDate(request.EpisodeTitle, request.Released,
                    podcastEpisodes.Episodes);
        }

        if (matchingEpisode != null)
        {
            var showRequest = new EpisodeRequest { Market = market };
            fullEpisode = await spotifyClientWrapper.GetFullEpisode(matchingEpisode.Id, showRequest, indexingContext);
        }

        return new FindEpisodeResponse(TakeIfFree(fullEpisode), podcastEpisodes.ExpensiveQueryFound);
    }

    private FullEpisode? TakeIfFree(FullEpisode? episode)
    {
        if (episode == null || episode.IsSpotifyFree())
        {
            return episode;
        }

        logger.LogWarning(
            "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false).",
            episode.Id,
            episode.Name);
        return null;
    }
}
