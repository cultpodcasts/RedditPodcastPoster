using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyPodcastEnricher(
    ISpotifyEpisodeResolver spotifyIdResolver,
    ISpotifyPodcastResolver spotifyPodcastResolver,
    ILogger<SpotifyPodcastEnricher> logger)
    : ISpotifyPodcastEnricher
{
    public async Task<bool> AddIdAndUrls(Podcast podcast, IndexingContext indexingContext)
    {
        var podcastShouldUpdate = false;
        if (string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var matchedPodcast =
                await spotifyPodcastResolver.FindPodcast(podcast.ToFindSpotifyPodcastRequest(), indexingContext);
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
            foreach (var podcastEpisode in podcast.Episodes)
            {
                if (string.IsNullOrWhiteSpace(podcastEpisode.SpotifyId))
                {
                    var findEpisodeResponse = await spotifyIdResolver.FindEpisode(
                        FindSpotifyEpisodeRequestFactory.Create(podcast, podcastEpisode), indexingContext);
                    if (!string.IsNullOrWhiteSpace(findEpisodeResponse.FullEpisode?.Id))
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