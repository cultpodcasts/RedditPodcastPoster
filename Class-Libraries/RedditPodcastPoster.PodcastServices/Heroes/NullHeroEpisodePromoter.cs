using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;

namespace RedditPodcastPoster.PodcastServices.Heroes;

/// <summary>No-op promoter used unless EdgeApi registers a real implementation.</summary>
public sealed class NullHeroEpisodePromoter : IHeroEpisodePromoter
{
    public Task PromoteAsync(IReadOnlyList<Guid> episodeIds, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
