using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Twitter;

public class TweetPoster : ITweetPoster
{
    private readonly ILogger<TweetPoster> _logger;
    private readonly IPodcastRepository _repository;
    private readonly ITweetBuilder _tweetBuilder;
    private readonly ITwitterClient _twitterClient;

    public TweetPoster(
        IPodcastRepository repository,
        ITweetBuilder tweetBuilder,
        ITwitterClient twitterClient,
        ILogger<TweetPoster> logger)
    {
        _repository = repository;
        _tweetBuilder = tweetBuilder;
        _twitterClient = twitterClient;
        _logger = logger;
    }

    public async Task PostTweet(PodcastEpisode podcastEpisode)
    {
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
                await _repository.Update(podcastEpisode.Podcast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"Failure to save podcast with podcast-id '{podcastEpisode.Podcast.Id}' to update episode with id '{podcastEpisode.Episode.Id}'.");
                throw;
            }
        }
        else
        {
            var message =
                $"Could not post tweet for podcast-episode: Podcast-id: '{podcastEpisode.Podcast.Id}', Episode-id: '{podcastEpisode.Episode.Id}'. Tweet: '{tweet}'.";
            _logger.LogError(message);
            throw new Exception(message);
        }
    }
}