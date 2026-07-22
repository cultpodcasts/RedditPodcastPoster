using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;

namespace Api.Services.Episodes;

public class EpisodeOutgoingService(
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    ILogger<EpisodeOutgoingService> logger) : IEpisodeOutgoingService
{
    public Task<EpisodeOutgoingResult> GetOutgoingAsync(
        string? days,
        string? posted,
        string? tweeted,
        string? blueskyPosted,
        CancellationToken cancellationToken)
    {
        var query = OutgoingEpisodesQuery.Parse(days, posted, tweeted, blueskyPosted);
        return GetOutgoingAsync(query, cancellationToken);
    }

    public async Task<EpisodeOutgoingResult> GetOutgoingAsync(
        OutgoingEpisodesQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var episodes = new List<EpisodePodcastPair>();
            var since = DateTimeExtensions.DaysAgo(query.Days);

            var podcastEpisodes = await recentEpisodeCandidatesProvider.GetEpisodes(since);

            foreach (var podcastEpisode in podcastEpisodes)
            {
                var episode = podcastEpisode.Episode;
                var podcast = podcastEpisode.Podcast;

                if (episode.Removed)
                {
                    continue;
                }

                if (episode.Posted && !query.Posted)
                {
                    continue;
                }

                if (episode.Tweeted && !query.Tweeted)
                {
                    continue;
                }

                if (episode.BlueskyPosted.HasValue && episode.BlueskyPosted.Value && !query.BlueskyPosted)
                {
                    continue;
                }

                episodes.Add(new EpisodePodcastPair(episode, podcast));
            }

            return new EpisodeOutgoingResult(
                EpisodeOutgoingStatus.Ok,
                episodes.OrderByDescending(x => x.Episode.Release).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get out-going episodes.", nameof(GetOutgoingAsync));
            return new EpisodeOutgoingResult(EpisodeOutgoingStatus.Failed);
        }
    }
}
