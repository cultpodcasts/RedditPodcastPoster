using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Repositories;

namespace EpisodeServiceBackfill;

/// <summary>
/// Temporary backfill client. Subclasses the live repository for container/DI shape, but the
/// services/ids patch lives only here — production <see cref="IEpisodeRepository"/> has no such method.
/// </summary>
public sealed class BackFillEpisodeRepository : EpisodeRepository, IBackfillEpisodeRepository
{
    private readonly Container _container;

    public BackFillEpisodeRepository(
        Container container,
        ILookupRepository lookupRepository,
        IPodcastRepository podcastRepository,
        ILogger<EpisodeRepository> logger)
        : base(container, lookupRepository, podcastRepository, logger)
    {
        _container = container;
    }

    public async Task<bool> PatchServicesAndIds(
        Guid podcastId,
        Guid episodeId,
        Dictionary<string, EpisodeServiceLink>? services,
        EpisodeIds? ids)
    {
        if (podcastId == Guid.Empty)
        {
            throw new InvalidOperationException("podcastId must be set before patching services/ids.");
        }

        if (episodeId == Guid.Empty)
        {
            throw new InvalidOperationException("episodeId must be set before patching services/ids.");
        }

        var operations = new List<PatchOperation>(2);
        if (services is { Count: > 0 })
        {
            operations.Add(PatchOperation.Set("/services", services));
        }

        if (ids is not null && !ids.IsEmpty)
        {
            operations.Add(PatchOperation.Set("/ids", ids));
        }

        if (operations.Count == 0)
        {
            return true;
        }

        try
        {
            await _container.PatchItemAsync<Episode>(
                episodeId.ToString(),
                new PartitionKey(podcastId.ToString()),
                operations);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
