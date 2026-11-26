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
    public async Task<EpisodeAwaitingApprovalResult> GetAwaitingApprovalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var held = await episodeRepository
                .GetAllBy(EpisodeCosmosFilters.AwaitingApproval)
                .ToListAsync(cancellationToken);

            var podcasts = new Dictionary<Guid, Podcast>();
            var pairs = new List<EpisodePodcastPair>();
            foreach (var episode in held.OrderByDescending(e => e.ReleaseUtc))
            {
                pairs.Add(new EpisodePodcastPair(episode, await PodcastFor(episode, podcasts)));
            }

            return new EpisodeAwaitingApprovalResult(EpisodeAwaitingApprovalStatus.Ok, pairs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to list episodes awaiting approval.", nameof(GetAwaitingApprovalAsync));
            return new EpisodeAwaitingApprovalResult(EpisodeAwaitingApprovalStatus.Failed);
        }
    }

    private async Task<Podcast> PodcastFor(Episode episode, Dictionary<Guid, Podcast> podcasts)
    {
        if (podcasts.TryGetValue(episode.PodcastId, out var cached))
        {
            return cached;
        }

        var stored = await podcastRepository.GetPodcast(episode.PodcastId);
        if (stored is null)
        {
            logger.LogWarning(
                "{method}: episode {EpisodeId} awaiting approval has no podcast {PodcastId}; listing it under the name stored on the episode.",
                nameof(GetAwaitingApprovalAsync),
                episode.Id,
                episode.PodcastId);
            stored = new Podcast
            {
                Id = episode.PodcastId,
                Name = episode.PodcastName ?? string.Empty
            };
        }

        podcasts[episode.PodcastId] = stored;
        return stored;
    }
}
