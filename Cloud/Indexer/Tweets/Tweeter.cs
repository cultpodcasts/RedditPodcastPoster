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
            var tooManyRequests = false;
            foreach (var podcastEpisode in untweeted)
            {
                if (tweeted || tooManyRequests)
                {
                    break;
                }

                try
                {
                    var tweetStatus = await tweetPoster.PostTweet(podcastEpisode);
                    tweeted = tweetStatus == TweetSendStatus.Sent;
                    tooManyRequests = tweetStatus == TweetSendStatus.TooManyRequests;
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
        var podcastEpisodes = new List<PodcastEpisode>();

        var untweetedPodcastIds =
            await repository.GetPodcastsIdsWithUnpostedReleasedSince(DateTime.UtcNow.AddDays(-39));

        foreach (var untweetedPodcastId in untweetedPodcastIds)
        {
            var podcast = await repository.GetPodcast(untweetedPodcastId);
            var filtered = podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast, youTubeRefreshed, spotifyRefreshed);
            podcastEpisodes.AddRange(filtered);
        }

        return podcastEpisodes;
    }
}