using Microsoft.Extensions.Logging;
using Poster;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;

public class PostProcessor
{
    private readonly IContentPublisher _contentPublisher;
    private readonly ILogger<PostProcessor> _logger;
    private readonly IPodcastEpisodeFilter _podcastEpisodeFilter;
    private readonly IPodcastEpisodesPoster _podcastEpisodesPoster;
    private readonly IProcessResponsesAdaptor _processResponsesAdaptor;
    private readonly IPodcastRepository _repository;
    private readonly ITweetPoster _tweetPoster;

    public PostProcessor(
        IPodcastRepository repository,
        IPodcastEpisodesPoster podcastEpisodesPoster,
        IProcessResponsesAdaptor processResponsesAdaptor,
        IContentPublisher contentPublisher,
        IPodcastEpisodeFilter podcastEpisodeFilter,
        ITweetPoster tweetPoster,
        ILogger<PostProcessor> logger)
    {
        _repository = repository;
        _podcastEpisodesPoster = podcastEpisodesPoster;
        _processResponsesAdaptor = processResponsesAdaptor;
        _contentPublisher = contentPublisher;
        _podcastEpisodeFilter = podcastEpisodeFilter;
        _tweetPoster = tweetPoster;
        _logger = logger;
    }

    public async Task Process(PostRequest request)
    {
        IList<Podcast> podcasts;
        if (request.PodcastId.HasValue)
        {
            var podcast = await _repository.GetPodcast(request.PodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.PodcastId.Value}' not found.");
            }

            podcasts = new[] {podcast};
        }
        else
        {
            podcasts = await _repository.GetAll().ToListAsync();
        }

        await PostNewEpisodes(request, podcasts);
        Task[] publishingTasks;
        if (request.PublishSubjects)
        {
            publishingTasks = new[]
            {
                _contentPublisher.PublishHomepage()
            };
        }
        else
        {
            publishingTasks = new[]
            {
                _contentPublisher.PublishHomepage()
            };
        }

        await Task.WhenAll(publishingTasks);
        if (!request.SkipTweet)
        {
            var untweeted =
                _podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(podcasts, numberOfDays: request.ReleasedWithin);

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

    private async Task PostNewEpisodes(PostRequest request, IList<Podcast> podcasts)
    {
        var results =
            await _podcastEpisodesPoster.PostNewEpisodes(
                DateTime.UtcNow.AddDays(-1 * request.ReleasedWithin),
                podcasts,
                preferYouTube: request.YouTubePrimaryPostService);
        var result = _processResponsesAdaptor.CreateResponse(results);
        if (!result.Success)
        {
            _logger.LogError(result.ToString());
        }
        else
        {
            _logger.LogInformation(result.ToString());
        }
    }
}