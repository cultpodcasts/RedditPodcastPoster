using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;
using RedditPodcastPoster.UrlShortening;
using Episode = RedditPodcastPoster.Models.Episode;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace Tweet;

public class TweetProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITweetBuilder tweetBuilder,
    ITwitterClient twitterClient,
    IShortnerService shortnerService,
    ILogger<TweetProcessor> logger)
{
    public async Task Run(TweetRequest request)
    {
        var podcast = await podcastRepository.GetPodcast(request.PodcastId);
        if (podcast != null)
        {
            if (podcast.Removed == true)
            {
                var message =
                    $"Podcast '{podcast.Name}' with id '{podcast.Id}' is removed and cannot be tweeted.";
                logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            var podcastEpisodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();
            var mostRecentEpisode =
                podcastEpisodes
                    .Where(x => x is { Tweeted: false, Ignored: false, Removed: false })
                    .MaxBy(x => x.Release);

            if (mostRecentEpisode != null)
            {
                var podcastEpisode = CreatePodcastEpisode(podcast, mostRecentEpisode);
                var shortnerResult = await shortnerService.Write(podcastEpisode);
                if (!shortnerResult.Success)
                {
                    logger.LogError("Unsuccessful shortening-url.");
                }

                var tweet = await tweetBuilder.BuildTweet(podcastEpisode, shortnerResult.Url);
                var tweetStatus = await twitterClient.Send(tweet);
                var tweeted = tweetStatus.TweetSendStatus == TweetSendStatus.Sent;

                if (tweeted)
                {
                    mostRecentEpisode.Tweeted = true;
                    try
                    {
                        await episodeRepository.Save(mostRecentEpisode);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Failure to save episode with id '{EpisodeId}' for podcast-id '{PodcastId}'.",
                            mostRecentEpisode.Id, podcast.Id);
                        throw;
                    }
                }
                else
                {
                    var message =
                        $"Could not post tweet for podcast-episode: Podcast-id: '{podcastEpisode.Podcast.Id}', Episode-id: '{podcastEpisode.Episode.Id}'. Tweet: '{tweet}'.";
                    logger.LogError(message);
                    throw new Exception(message);
                }
            }
            else
            {
                var message =
                    $"Could not find an episode for podcast '{podcast.Name}' with id: '{podcast.Id}'.";
                logger.LogError(message);
                throw new Exception(message);
            }
        }
        else
        {
            var message =
                $"Could not find an podcast with id: '{request.PodcastId}'.";
            logger.LogError(message);
            throw new Exception(message);
        }
    }

    private static PodcastEpisode CreatePodcastEpisode(Podcast podcast, Episode episode)
    {
        return new PodcastEpisode(podcast, episode);
    }
}
