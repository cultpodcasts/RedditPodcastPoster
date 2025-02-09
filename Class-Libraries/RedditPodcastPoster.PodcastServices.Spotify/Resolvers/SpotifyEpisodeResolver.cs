using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public class SpotifyEpisodeResolver(
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
    ISpotifyClientWrapper spotifyClientWrapper,
    ISearchResultFinder searchResultFinder,
    ILogger<SpotifyEpisodeResolver> logger)
    : ISpotifyEpisodeResolver
{
    private static readonly TimeSpan YouTubeAuthorityToAudioReleaseConsiderationThreshold = TimeSpan.FromDays(14);

    public async Task<FindEpisodeResponse> FindEpisode(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext)
    {
        var market = request.Market ?? Market.CountryCode;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(FindEpisode)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastSpotifyId}', Podcast-Name:'{request.PodcastName}', Episode-Id:'{request.EpisodeSpotifyId}', Episode-Name:'{request.EpisodeTitle}'.");
            return new FindEpisodeResponse(null);
        }

        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            var episodeRequest = new EpisodeRequest
                {Market = market};
            fullEpisode =
                await spotifyClientWrapper.GetFullEpisode(request.EpisodeSpotifyId, episodeRequest, indexingContext);
            if (fullEpisode != null)
            {
                return new FindEpisodeResponse(fullEpisode);
            }
        }

        var podcastEpisodes = await spotifyPodcastEpisodesProvider.GetAllEpisodes(request, indexingContext, market);

        SimpleEpisode? matchingEpisode;
        if (request is {ReleaseAuthority: Service.YouTube, Length: not null})
        {
            var ticks = YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
            if (request.YouTubePublishingDelay.HasValue &&
                request.YouTubePublishingDelay.Value != TimeSpan.Zero)
            {
                var delayTicks = request.YouTubePublishingDelay.Value.Ticks;
                if (delayTicks < 0)
                {
                    ticks = Math.Abs(delayTicks);
                }
            }

            matchingEpisode = searchResultFinder.FindMatchingEpisodeByLength(
                request.EpisodeTitle,
                request.Length.Value,
                podcastEpisodes.Episodes,
                y => request.Released.HasValue &&
                     Math.Abs((y.GetReleaseDate() - request.Released.Value).Ticks) < ticks);
        }
        else
        {
            matchingEpisode =
                searchResultFinder.FindMatchingEpisodeByDate(request.EpisodeTitle, request.Released,
                    podcastEpisodes.Episodes);
        }

        if (matchingEpisode != null)
        {
            var showRequest = new EpisodeRequest {Market = market};
            fullEpisode = await spotifyClientWrapper.GetFullEpisode(matchingEpisode.Id, showRequest, indexingContext);
        }

        return new FindEpisodeResponse(fullEpisode, podcastEpisodes.ExpensiveQueryFound);
    }
}