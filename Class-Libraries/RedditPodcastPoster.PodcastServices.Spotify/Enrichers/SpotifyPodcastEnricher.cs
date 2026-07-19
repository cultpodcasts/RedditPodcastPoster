using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

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

                if (matchedPodcast.ExpensiveQueryFound)
                {
                    podcast.SpotifyEpisodesQueryIsExpensive = true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            foreach (var podcastEpisode in episodes)
            {
                if (string.IsNullOrWhiteSpace(podcastEpisode.SpotifyId))
                {
                    var findEpisodeResponse = await spotifyIdResolver.FindEpisode(
                        FindSpotifyEpisodeRequestFactory.Create(
                            podcast,
                            podcastEpisode),
                        indexingContext);
                    if (findEpisodeResponse.FullEpisode != null &&
                        !findEpisodeResponse.FullEpisode.IsSpotifyFree())
                    {
                        logger.LogWarning(
                            "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false, restrictions.reason={RestrictionReason}).",
                            findEpisodeResponse.FullEpisode.Id,
                            findEpisodeResponse.FullEpisode.Name,
                            findEpisodeResponse.FullEpisode.GetSpotifyRestrictionReason());
                    }
                    else if (!string.IsNullOrWhiteSpace(findEpisodeResponse.FullEpisode?.Id))
                    {
                        podcastEpisode.SpotifyId = findEpisodeResponse.FullEpisode.Id;
                        podcastShouldUpdate = true;
                    }

                    if (findEpisodeResponse.IsExpensiveQuery)
                    {
                        podcast.SpotifyEpisodesQueryIsExpensive = true;
                    }
                }
            }
        }

        return podcastShouldUpdate;
    }

}