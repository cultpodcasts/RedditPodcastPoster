using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor(
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IProcessResponsesAdaptor processResponsesAdaptor,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<EpisodeProcessor> logger)
    : IEpisodeProcessor
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<ProcessResponse> PostEpisodesSinceReleaseDate(
        DateTime since,
        int? maxPosts,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("{PostEpisodesSinceReleaseDateName} Finding episodes released since '{DateTime}'.",
            nameof(PostEpisodesSinceReleaseDate), since);

        var redditReleasedSince = DateTimeExtensions.DaysAgo(_postingCriteria.RedditDays);

        var podcastIds = (await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(redditReleasedSince))
            .Where(x => !x.Posted)
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