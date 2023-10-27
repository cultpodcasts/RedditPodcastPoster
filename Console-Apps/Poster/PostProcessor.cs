using Microsoft.Extensions.Logging;
using Poster;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

public class PostProcessor
{
    private readonly IContentPublisher _contentPublisher;
    private readonly IPodcastEpisodeFilter _podcastEpisodeFilter;
    private readonly ILogger<PostProcessor> _logger;
    private readonly IPodcastEpisodesPoster _podcastEpisodesPoster;
    private readonly IProcessResponsesAdaptor _processResponsesAdaptor;
    private readonly IPodcastRepository _repository;

    public PostProcessor(
        IPodcastRepository repository,
        IPodcastEpisodesPoster podcastEpisodesPoster,
        IProcessResponsesAdaptor processResponsesAdaptor,
        IContentPublisher contentPublisher,
        IPodcastEpisodeFilter podcastEpisodeFilter,
        ILogger<PostProcessor> logger)
    {
        _repository = repository;
        _podcastEpisodesPoster = podcastEpisodesPoster;
        _processResponsesAdaptor = processResponsesAdaptor;
        _contentPublisher = contentPublisher;
        _podcastEpisodeFilter = podcastEpisodeFilter;
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
        await Publish(podcasts);
        if (!request.SkipTweet)
        {
            var postToTweet = _podcastEpisodeFilter.GetMostRecentUntweetedEpisode(podcasts);
        }
    }

    private async Task Publish(IList<Podcast> podcasts)
    {
        await _contentPublisher.Publish();
    }

    private async Task PostNewEpisodes(PostRequest request, IList<Podcast> podcasts)
    {
        var results =
            await _podcastEpisodesPoster.PostNewEpisodes(DateTime.UtcNow.AddDays(-1 * request.ReleasedWithin),
                podcasts);
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