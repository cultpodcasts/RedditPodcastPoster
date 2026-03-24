using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor(
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
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
        var podcastIds = (await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(unpostedEpisodeThreshold))
            .Where(x =>
                !x.Posted &&
                (!x.PodcastRemoved.IsDefined() || x.PodcastRemoved == false || x.PodcastRemoved == null))
            .Select(x => x.PodcastId)
            .Distinct()
            .ToArray();

        var matchingPodcastEpisodeResults = await podcastEpisodesPoster.PostNewEpisodes(
            since,
            podcastIds,
            youTubeRefreshed,
            spotifyRefreshed,
            maxPosts: maxPosts);

        return processResponsesAdaptor.CreateResponse(matchingPodcastEpisodeResults);
    }
}