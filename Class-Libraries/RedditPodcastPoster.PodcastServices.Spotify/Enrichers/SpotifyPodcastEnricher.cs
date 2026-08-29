using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public class SpotifyPodcastEnricher(
    ISpotifyEpisodeResolver spotifyIdResolver,
    ISpotifyPodcastResolver spotifyPodcastResolver,
    ILogger<SpotifyPodcastEnricher> logger)
    : ISpotifyPodcastEnricher
{
    public async Task<bool> AddIdAndUrls(Podcast podcast, IEnumerable<Episode> episodes, IndexingContext indexingContext)
    {
        var podcastShouldUpdate = false;
        if (string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var matchedPodcast =
                await spotifyPodcastResolver.FindPodcast(podcast.ToFindSpotifyPodcastRequest(episodes), indexingContext);
            if (matchedPodcast != null)
            {
                if (!string.IsNullOrWhiteSpace(matchedPodcast.Id))
                {
                    podcast.SpotifyId = matchedPodcast.Id;
                    podcastShouldUpdate = true;
                }

                if (matchedPodcast.ExpensiveQueryFound.HasValue)
                {
                    SpotifyExpensiveQueryFlag.Apply(
                        podcast,
                        matchedPodcast.ExpensiveQueryFound,
                        SpotifyExpensiveQueryFlag.MinimumOrderSampleSize,
                        logger);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            foreach (var podcastEpisode in episodes)
            {
                if (string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(podcastEpisode)))
                {
                    var findEpisodeResponse = await spotifyIdResolver.FindEpisode(
                        FindSpotifyEpisodeRequestFactory.Create(
                            podcast,
                            podcastEpisode),
                        indexingContext);
                    if (findEpisodeResponse.FullEpisode != null &&
                        !findEpisodeResponse.FullEpisode.IsSpotifyFree())
                    {
                        SpotifyNonPlayableSkipLogger.Log(
                            logger,
                            findEpisodeResponse.FullEpisode,
                            Market.CountryCode);
                    }
                    else if (!string.IsNullOrWhiteSpace(findEpisodeResponse.FullEpisode?.Id))
                    {
                        EpisodeServicePresence.SetSpotifyIdentity(podcastEpisode, findEpisodeResponse.FullEpisode.Id);
                        podcastShouldUpdate = true;
                    }

                    if (findEpisodeResponse.IsExpensiveQuery.HasValue)
                    {
                        SpotifyExpensiveQueryFlag.Apply(
                            podcast,
                            findEpisodeResponse.IsExpensiveQuery,
                            SpotifyExpensiveQueryFlag.MinimumOrderSampleSize,
                            logger);
                    }
                }
            }
        }

        return podcastShouldUpdate;
    }

}
