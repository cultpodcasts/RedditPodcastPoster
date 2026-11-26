using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Episodes;

public class EpisodeAwaitingApprovalService(
    IEpisodeRepository episodeRepository,
    IPodcastRepository podcastRepository,
    ILogger<EpisodeAwaitingApprovalService> logger) : IEpisodeAwaitingApprovalService
{
    /// <summary>
    /// The expected set is small: only split-offs held by a submit overwrite (#1416). Above this the
    /// count is logged at Warning so a runaway set (for example a bulk-hold regression) shows in App Insights.
    /// </summary>
    internal const int ExpectedMaximumAwaitingApproval = 100;

    public async Task<EpisodeAwaitingApprovalResult> GetAwaitingApprovalAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Unbounded by design: held split-offs only (see ExpectedMaximumAwaitingApproval).
            var held = await episodeRepository
                .GetAllBy(EpisodeCosmosFilters.AwaitingApproval)
                .ToListAsync(cancellationToken);

            logger.Log(
                held.Count > ExpectedMaximumAwaitingApproval ? LogLevel.Warning : LogLevel.Information,
                "{method}: {count} episode(s) awaiting approval (expected at most {expectedMaximum}).",
                nameof(GetAwaitingApprovalAsync),
                held.Count,
                ExpectedMaximumAwaitingApproval);

            var podcasts = new Dictionary<Guid, Podcast?>();
            var pairs = new List<EpisodePodcastPair>();
            foreach (var episode in held.OrderByDescending(e => e.ReleaseUtc))
            {
                var podcast = await PodcastFor(episode, podcasts);
                if (podcast is null)
                {
                    // The editor GET and the approve/reject PATCH both fail without a stored podcast,
                    // so an orphan cannot be actioned from the list. Same as the other curator lists: skip and log.
                    logger.LogWarning(
                        "{method}: episode {EpisodeId} awaiting approval has no podcast {PodcastId}; not listing it.",
                        nameof(GetAwaitingApprovalAsync),
                        episode.Id,
                        episode.PodcastId);
                    continue;
                }

                pairs.Add(new EpisodePodcastPair(episode, podcast));
            }

            return new EpisodeAwaitingApprovalResult(EpisodeAwaitingApprovalStatus.Ok, pairs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to list episodes awaiting approval.", nameof(GetAwaitingApprovalAsync));
            return new EpisodeAwaitingApprovalResult(EpisodeAwaitingApprovalStatus.Failed);
        }
    }

    private async Task<Podcast?> PodcastFor(Episode episode, Dictionary<Guid, Podcast?> podcasts)
    {
        if (podcasts.TryGetValue(episode.PodcastId, out var cached))
        {
            return cached;
        }

        var stored = await podcastRepository.GetPodcast(episode.PodcastId);
        podcasts[episode.PodcastId] = stored;
        return stored;
    }
}
