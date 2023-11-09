using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor : IEpisodeProcessor
{
    private readonly ILogger<EpisodeProcessor> _logger;
    private readonly IPodcastEpisodesPoster _podcastEpisodesPoster;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IProcessResponsesAdaptor _processResponsesAdaptor;

    public EpisodeProcessor(
        IPodcastRepository podcastRepository,
        IPodcastEpisodesPoster podcastEpisodesPoster,
        IProcessResponsesAdaptor processResponsesAdaptor,
        ILogger<EpisodeProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _podcastEpisodesPoster = podcastEpisodesPoster;
        _processResponsesAdaptor = processResponsesAdaptor;
        _logger = logger;
    }

    public async Task<ProcessResponse> PostEpisodesSinceReleaseDate(
        DateTime since, 
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        _logger.LogInformation($"{nameof(PostEpisodesSinceReleaseDate)} Finding episodes released since '{since}'.");
        var podcasts = await _podcastRepository.GetAll().ToListAsync();

        var matchingPodcastEpisodeResults =
            await _podcastEpisodesPoster.PostNewEpisodes(since, podcasts, youTubeRefreshed, spotifyRefreshed);

        return _processResponsesAdaptor.CreateResponse(matchingPodcastEpisodeResults);
    }
}