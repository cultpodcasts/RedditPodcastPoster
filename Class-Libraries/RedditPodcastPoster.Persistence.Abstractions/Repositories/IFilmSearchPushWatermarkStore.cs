using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>Reads and saves the <see cref="FilmSearchPushWatermark"/> LookUps singleton.</summary>
public interface IFilmSearchPushWatermarkStore
{
    /// <returns>The saved watermark, or null when none has been saved yet.</returns>
    Task<FilmSearchPushWatermark?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FilmSearchPushWatermark watermark, CancellationToken cancellationToken = default);
}
