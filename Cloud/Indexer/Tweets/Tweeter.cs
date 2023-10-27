using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;

namespace Indexer.Tweets;

public class Tweeter : ITweeter
{
    private readonly ILogger<Tweeter> _logger;
    private readonly IPodcastEpisodeFilter _podcastEpisodeFilter;
    private readonly IPodcastRepository _repository;
    private readonly ITweetPoster _tweetPoster;

    public Tweeter(
        IPodcastRepository repository,
        IPodcastEpisodeFilter podcastEpisodeFilter,
        ITweetPoster tweetPoster,
        ILogger<Tweeter> logger)
    {
        _repository = repository;
        _podcastEpisodeFilter = podcastEpisodeFilter;
        _tweetPoster = tweetPoster;
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
                await _tweetPoster.PostTweet(podcastEpisode);
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

        return _podcastEpisodeFilter.GetMostRecentUntweetedEpisode(podcasts);
    }
}