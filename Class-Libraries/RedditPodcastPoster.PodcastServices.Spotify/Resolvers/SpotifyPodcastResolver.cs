using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public class SpotifyPodcastResolver(
    ISpotifyClientWrapper spotifyClientWrapper,
    ISearchResultFinder searchResultFinder,
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
    ILogger<SpotifyPodcastResolver> logger)
    : ISpotifyPodcastResolver
{
    public async Task<SpotifyPodcastWrapper?> FindPodcast(
        FindSpotifyPodcastRequest request,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(FindPodcast)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastId}', Podcast-Name:'{request.Name}'.");
            return null;
        }

        SimpleShow? matchingSimpleShow = null;
        FullShow? matchingFullShow = null;
        var expensiveSpotifyEpisodesQueryFound = false;
        if (!string.IsNullOrWhiteSpace(request.PodcastId))
        {
            var showRequest = new ShowRequest {Market = Market.CountryCode};
            matchingFullShow = await spotifyClientWrapper.GetFullShow(request.PodcastId, showRequest, indexingContext);
        }

        if (matchingFullShow == null)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.Name);
            var simpleShows = await spotifyClientWrapper.GetSimpleShows(searchRequest, indexingContext);
            if (simpleShows.Any())
            {
                var matchingPodcasts = searchResultFinder.FindMatchingPodcasts(request.Name, simpleShows);
                if (request.Episodes.Any())
                {
                    foreach (var candidatePodcast in matchingPodcasts)
                    {
                        var showEpisodesRequest = new ShowEpisodesRequest {Market = Market.CountryCode};
                        if (indexingContext.ReleasedSince.HasValue)
                        {
                            showEpisodesRequest.Limit = 1;
                        }

                        var podcastEpisodes = await spotifyPodcastEpisodesProvider.GetEpisodes(
                            new GetEpisodesRequest(new SpotifyPodcastId(candidatePodcast.Id),
                                showEpisodesRequest.Market), indexingContext);

                        if (podcastEpisodes.ExpensiveQueryFound)
                        {
                            expensiveSpotifyEpisodesQueryFound = true;
                        }

                        if (podcastEpisodes.Episodes.Any())
                        {
                            var mostRecentEpisode = request.Episodes.OrderByDescending(x => x.Release).First();
                            var matchingEpisode =
                                searchResultFinder.FindMatchingEpisodeByDate(
                                    mostRecentEpisode.Title.Trim(),
                                    mostRecentEpisode.Release,
                                    podcastEpisodes.Episodes);
                            if (request.Episodes
                                .Select(x => x.Url?.ToString())
                                .Contains(matchingEpisode!.ExternalUrls.FirstOrDefault().Value))
                            {
                                matchingSimpleShow = candidatePodcast;
                                break;
                            }
                        }
                    }
                }

                matchingSimpleShow ??=
                    FuzzyMatcher.Match(request.Name, matchingPodcasts, x => x.Name);
            }
        }

        return new SpotifyPodcastWrapper(matchingFullShow, matchingSimpleShow, expensiveSpotifyEpisodesQueryFound);
    }
}