using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Subjects.Providers;

namespace Api.Services.Episodes;

public class EpisodeOutgoingService(
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    ICachedSubjectProvider subjectsProvider,
    EpisodeDiscreteMapper episodeDiscreteMapper,
    ILogger<EpisodeOutgoingService> logger) : IEpisodeOutgoingService
{
    public async Task<EpisodeOutgoingResult> GetOutgoingAsync(
        OutgoingEpisodesQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var episodes = new List<Dtos.DiscreteEpisode>();
            var since = DateTimeExtensions.DaysAgo(query.Days);
            var subjects = await subjectsProvider.GetAll().ToListAsync(cancellationToken);

            // Use cached episodes from provider instead of cross-partition query
            var podcastEpisodes = await recentEpisodeCandidatesProvider.GetEpisodes(since);

            foreach (var podcastEpisode in podcastEpisodes)
            {
                var episode = podcastEpisode.Episode;
                var podcast = podcastEpisode.Podcast;

                // Skip removed
                if (episode.Removed)
                {
                    continue;
                }

                // Skip posted episodes if not explicitly requested
                if (episode.Posted && !query.Posted)
                {
                    continue;
                }

                // Skip tweeted episodes if not explicitly requested
                if (episode.Tweeted && !query.Tweeted)
                {
                    continue;
                }

                // Skip bluesky-posted episodes if not explicitly requested
                if (episode.BlueskyPosted.HasValue && episode.BlueskyPosted.Value && !query.BlueskyPosted)
                {
                    continue;
                }

                episodes.Add(await episodeDiscreteMapper.ToDiscreteEpisode(episode, podcast, subjects));
            }

            return new EpisodeOutgoingResult(
                EpisodeOutgoingStatus.Ok,
                episodes.OrderByDescending(x => x.Release).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get out-going episodes.", nameof(GetOutgoingAsync));
            return new EpisodeOutgoingResult(EpisodeOutgoingStatus.Failed);
        }
    }
}
