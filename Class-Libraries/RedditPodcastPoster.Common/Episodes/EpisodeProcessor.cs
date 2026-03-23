using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor(
    IEpisodeRepository episodeRepository,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IProcessResponsesAdaptor processResponsesAdaptor,
    ILogger<EpisodeProcessor> logger)
    : IEpisodeProcessor
{
    public async Task<ProcessResponse> PostEpisodesSinceReleaseDate(
        DateTime since,
        int? maxPosts,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("{PostEpisodesSinceReleaseDateName} Finding episodes released since '{DateTime}'.",
            nameof(PostEpisodesSinceReleaseDate), since);

        var unpostedEpisodeThreshold = DateTimeExtensions.DaysAgo(7);
        var podcastIds = (await episodeRepository
                .GetAllBy(x =>
                        x.Release >= unpostedEpisodeThreshold &&
                        !x.Posted &&
                        !x.Ignored &&
                        !x.Removed &&
                        (!x.PodcastRemoved.IsDefined() || x.PodcastRemoved == false),
                    x => x.PodcastId)
                .ToListAsync())
            .Distinct()
            .ToArray();

        var matchingPodcastEpisodeResults = await podcastEpisodesPoster.PostNewEpisodes(since, podcastIds,
            youTubeRefreshed, spotifyRefreshed, maxPosts: maxPosts);

        return processResponsesAdaptor.CreateResponse(matchingPodcastEpisodeResults);
    }
}