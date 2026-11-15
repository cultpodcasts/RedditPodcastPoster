using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>Reads and saves the <see cref="FilmSearchPushWatermark"/> LookUps singleton.</summary>
/// <remarks>Used by FilmSearchPusher, the only bulk film search push. Do not remove until search moves off Azure AI Search to Cloudflare D1 and films are indexed there (interim: schedule the film push, #1408). See <c>.cursor/rules/film-search-pusher-keep.mdc</c>.</remarks>
public interface IFilmSearchPushWatermarkStore
{
    /// <returns>The saved watermark, or null when none has been saved yet.</returns>
    Task<FilmSearchPushWatermark?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FilmSearchPushWatermark watermark, CancellationToken cancellationToken = default);
}
