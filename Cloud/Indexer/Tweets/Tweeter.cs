using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;

namespace Indexer.Tweets;

public class Tweeter(
    IPodcastRepository repository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    ITweetPoster tweetPoster,
    ILogger<Tweeter> logger)
    : ITweeter
{
    public async Task Tweet(bool youTubeRefreshed, bool spotifyRefreshed)
    {
        IEnumerable<PodcastEpisode> untweeted;
        try
        {
            untweeted = await GetUntweetedPodcastEpisodes(youTubeRefreshed, spotifyRefreshed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        if (untweeted.Any())
        {
            var tweeted = false;
            foreach (var podcastEpisode in untweeted)
            {
                if (tweeted)
                {
                    break;
                }

                try
                {
                    await tweetPoster.PostTweet(podcastEpisode);
                    tweeted = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        $"Unable to tweet episode with id '{podcastEpisode.Episode.Id}' with title '{podcastEpisode.Episode.Title}' from podcast with id '{podcastEpisode.Podcast.Id}' and name '{podcastEpisode.Podcast.Name}'.");
                }
            }
        }
    }

    private async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        const int numberOfDays = 2;
        IEnumerable<Podcast> podcasts;
        try
        {
            var since = DateTime.UtcNow.Date.AddDays(-1 * numberOfDays);

            podcasts = await repository.GetAllBy(x =>
                (!x.Removed.IsDefined() || x.Removed == false) &&
                x.Episodes.Any(episode =>
                    episode.Release >= since &&
                    episode.Removed == false &&
                    episode.Ignored == false &&
                    episode.Tweeted == false
                )
            ).ToArrayAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to retrieve podcasts");
            throw;
        }

        return podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
            podcasts, youTubeRefreshed, spotifyRefreshed);
    }
}