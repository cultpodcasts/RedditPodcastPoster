using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

/// <summary>One amp-api episodes page and the link to the next (older, for newest-first catalogues) page.</summary>
public sealed record AppleEpisodePage(IReadOnlyList<AppleEpisode> Episodes, string? Next);

public interface IApplePodcastService
{
    public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext, CancellationToken cancellationToken = default);

    public Task<AppleEpisode?> GetEpisode(long episodeId, IndexingContext indexingContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the podcast's episodes: the first page when <paramref name="next"/> is null, else that link.
    /// Null when the request fails or the timeout guard fires.
    /// </summary>
    public Task<AppleEpisodePage?> GetEpisodePage(ApplePodcastId podcastId, string? next, CancellationToken cancellationToken = default);
}
