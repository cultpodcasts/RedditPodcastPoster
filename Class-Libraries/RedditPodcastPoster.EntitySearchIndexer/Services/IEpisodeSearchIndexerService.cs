using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.EntitySearchIndexer.Services;

public interface IEpisodeSearchIndexerService
{
    Task<EntitySearchIndexerResponse> IndexEpisode(Guid episodeId, CancellationToken cancellationToken);

    Task<EntitySearchIndexerResponse> IndexEpisode(
        Podcast podcast,
        Episode episode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="IndexEpisode(Podcast, Episode, CancellationToken)"/>. When
    /// <paramref name="youTubeAlternatesCleared"/> is true and the episode has no additional YouTube uploads, the
    /// search document sends <c>youTubeAlternates: []</c> so merge-or-upload clears a stale list (RPP #1526).
    /// </summary>
    Task<EntitySearchIndexerResponse> IndexEpisode(
        Podcast podcast,
        Episode episode,
        CancellationToken cancellationToken,
        bool youTubeAlternatesCleared);

    Task<EntitySearchIndexerResponse> IndexEpisodes(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken);
}
