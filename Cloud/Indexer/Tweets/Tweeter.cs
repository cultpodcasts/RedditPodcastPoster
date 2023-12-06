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

    public async Task Tweet(bool youTubeRefreshed, bool spotifyRefreshed)
    {
        IEnumerable<PodcastEpisode> untweeted;
        try
        {
            untweeted = await GetUntweetedPodcastEpisodes(youTubeRefreshed, spotifyRefreshed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure to find podcast-episode.");
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
                    await _tweetPoster.PostTweet(podcastEpisode);
                    tweeted = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"Unable to tweet episode with id '{podcastEpisode.Episode.Id}' with title '{podcastEpisode.Episode.Title}' from podcast with id '{podcastEpisode.Podcast.Id}' and name '{podcastEpisode.Podcast.Name}'.");
                }
            }
        }
    }

    private async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(bool youTubeRefreshed,
        bool spotifyRefreshed)
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

        return _podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(podcasts, youTubeRefreshed, spotifyRefreshed);
    }
}