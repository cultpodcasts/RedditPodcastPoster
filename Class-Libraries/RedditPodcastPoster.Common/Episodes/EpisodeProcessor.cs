using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor(
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IEpisodeRepository episodeRepository,
    IPodcastRepositoryV2 podcastRepository,
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

        var podcastEpisodes = (await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(redditReleasedSince))
            .Where(x => !x.Episode.Posted)
            .ToArray();

        var postingResult = await podcastEpisodesPoster.PostNewEpisodes(
            since,
            podcastEpisodes,
            youTubeRefreshed,
            spotifyRefreshed,
            maxPosts: maxPosts);

        // Persist modified episodes and podcasts (save each podcast only once)
        var savedPodcasts = new HashSet<Guid>();
        foreach (var podcastEpisode in postingResult.ModifiedPodcastEpisodes)
        {
            await episodeRepository.Save(podcastEpisode.Episode);
            if (savedPodcasts.Add(podcastEpisode.Podcast.Id))
            {
                await podcastRepository.Save(podcastEpisode.Podcast);
            }
        }

        return processResponsesAdaptor.CreateResponse(postingResult.Responses);
    }
}