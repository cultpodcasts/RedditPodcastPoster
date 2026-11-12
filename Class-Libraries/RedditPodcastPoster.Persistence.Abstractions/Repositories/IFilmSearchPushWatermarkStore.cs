using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>Reads and saves the <see cref="FilmSearchPushWatermark"/> LookUps singleton.</summary>
/// <remarks>Used by FilmSearchPusher, the only bulk film search push. Do not remove until the D1 search migration (search-platform epic) is online and films are indexed another way. See <c>.cursor/rules/film-search-pusher-keep.mdc</c>.</remarks>
public interface IFilmSearchPushWatermarkStore
{
    /// <returns>The saved watermark, or null when none has been saved yet.</returns>
    Task<FilmSearchPushWatermark?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FilmSearchPushWatermark watermark, CancellationToken cancellationToken = default);
}
