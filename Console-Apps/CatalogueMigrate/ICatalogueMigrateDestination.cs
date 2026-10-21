using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Models;

namespace CatalogueMigrate;

/// <summary>
/// Destination containers plus the search swap. Every write is an upsert or an
/// idempotent delete, so a batch can be re-run after a failure.
/// </summary>
public interface ICatalogueMigrateDestination
{
    Task<bool> HasTvShow(Guid tvShowId);
    Task<bool> HasNewsOrganisation(Guid newsOrganisationId);

    Task SaveNews(NewsOrganisation organisation, IReadOnlyList<NewsReport> reports);
    Task SaveTv(TvShow show, IReadOnlyList<TvShowEpisode> episodes);
    Task SaveFilm(Film film);

    /// <summary>Playables stored under the parent. Film: 1 when the film exists (parentId is the film id).</summary>
    Task<int> CountPlayables(string kind, Guid parentId);

    Task DeletePlayable(string kind, Guid parentId, Guid playableId);

    /// <summary>No-op for Film (no parent).</summary>
    Task DeleteParent(string kind, Guid parentId);

    Task<bool> UploadSearch(IReadOnlyList<EpisodeSearchRecord> documents, CancellationToken cancellationToken);
}
