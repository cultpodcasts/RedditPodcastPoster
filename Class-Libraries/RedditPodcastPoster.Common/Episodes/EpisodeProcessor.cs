using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor(
    IPodcastRepository podcastRepository,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IProcessResponsesAdaptor processResponsesAdaptor,
    ILogger<EpisodeProcessor> logger)
    : IEpisodeProcessor
{
    public async Task<ProcessResponse> PostEpisodesSinceReleaseDate(
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation($"{nameof(PostEpisodesSinceReleaseDate)} Finding episodes released since '{since}'.");
        var podcasts = await podcastRepository.GetPodcastsWithUnpostedEpisodesReleasedSince(since).ToArrayAsync();

        var matchingPodcastEpisodeResults =
            await podcastEpisodesPoster.PostNewEpisodes(since, podcasts, youTubeRefreshed, spotifyRefreshed);

        return processResponsesAdaptor.CreateResponse(matchingPodcastEpisodeResults);
    }
}