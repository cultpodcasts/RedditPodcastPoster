using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Twitter;

namespace Indexer.Tweets;

public class Tweeter : ITweeter
{
    private readonly ILogger<Tweeter> _logger;
    private readonly IPodcastRepository _repository;
    private readonly ITweetBuilder _tweetBuilder;
    private readonly ITwitterClient _twitterClient;

    public Tweeter(
        ITweetBuilder tweetBuilder,
        ITwitterClient twitterClient,
        IPodcastRepository repository,
        ILogger<Tweeter> logger)
    {
        _tweetBuilder = tweetBuilder;
        _twitterClient = twitterClient;
        _repository = repository;
        _logger = logger;
    }

    public async Task Tweet()
    {
        PodcastEpisode? podcastEpisode = null;
        try
        {
            podcastEpisode = await GetPodcastEpisode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        if (podcastEpisode != null)
        {
            try
            {
                await PostTweet(podcastEpisode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"Failure to post-tweet for podcast with id '{podcastEpisode.Podcast.Id}' and episode-id '{podcastEpisode.Episode.Id}'.");
                throw;
            }
        }
    }

    private async Task<PodcastEpisode?> GetPodcastEpisode()
    {
        List<Podcast> podcasts;
        try
        {
            podcasts = await _repository.GetAll().ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure to retrieve podcasts");
            throw;
        }

        var podcastEpisode =
            podcasts
                .SelectMany(p => p.Episodes.Select(e => new {Podcast = p, Episode = e}))
                .Where(x =>
                    x.Episode.Release >= DateTime.UtcNow.Date.AddHours(-24) &&
                    x.Episode is {Removed: false, Ignored: false, Tweeted: false} &&
                    (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                    !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
                .MaxBy(x => x.Episode.Release);
        if (podcastEpisode?.Podcast == null)
        {
            _logger.LogInformation("No Podcast-Episode found to Tweet.");
            return null;
        }

        return new PodcastEpisode(podcastEpisode.Podcast, podcastEpisode.Episode);
    }

    private async Task PostTweet(PodcastEpisode podcastEpisode)
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
}