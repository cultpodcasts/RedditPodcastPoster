using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Twitter;

namespace Tweet;

public class TweetProcessor
{
    private readonly ILogger<TweetProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly ITweetBuilder _tweetBuilder;
    private readonly ITwitterClient _twitterClient;

    public TweetProcessor(
        IPodcastRepository podcastRepository,
        ITweetBuilder tweetBuilder,
        ITwitterClient twitterClient,
        ILogger<TweetProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _tweetBuilder = tweetBuilder;
        _twitterClient = twitterClient;
        _logger = logger;
    }

    public async Task Run(TweetRequest request)
    {
        var podcast = await _podcastRepository.GetPodcast(request.PodcastId);
        if (podcast == null)
        {
            var mostRecentEpisode = podcast.Episodes.MaxBy(x => x.Release);

            if (mostRecentEpisode != null)
            {
                var podcastEpisode = new PodcastEpisode(podcast, mostRecentEpisode);
                var tweet = _tweetBuilder.BuildTweet(podcastEpisode);
                bool tweeted;
                try
                {
                    tweeted = await _twitterClient.Send(tweet);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"Failure to send tweet for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}', tweet: '{tweet}'.");
                    throw;
                }

                if (tweeted)
                {
                    podcastEpisode.Episode.Tweeted = true;
                    try
                    {
                        await _podcastRepository.Update(podcastEpisode.Podcast);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            $"Failure to save podcast with podcast-id '{podcastEpisode.Podcast.Id}' to update episode with id '{podcastEpisode.Episode.Id}'.");
                        throw;
                    }


                    _logger.LogInformation($"Tweeted '{tweet}'.");
                }
                else
                {
                    var message =
                        $"Could not post tweet for podcast-episode: Podcast-id: '{podcastEpisode.Podcast.Id}', Episode-id: '{podcastEpisode.Episode.Id}'. Tweet: '{tweet}'.";
                    _logger.LogError(message);
                    throw new Exception(message);
                }
            }
            else
            {
                var message =
                    $"Could not find an episode for podcast '{podcast.Name}' with id: '{podcast.Id}'.";
                _logger.LogError(message);
                throw new Exception(message);
            }
        }
        else
        {
            var message =
                $"Could not find an podcast with id: '{podcast.Id}'.";
            _logger.LogError(message);
            throw new Exception(message);
        }
    }
}