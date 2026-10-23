using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IFilmRepository : IRepository<Film>, IFilterableRepository<Film>
{
    Task<Film?> GetFilm(Guid filmId);
    Task Delete(Guid filmId);

    /// <summary>The film with its Cosmos ETag, for an optimistic <see cref="TryReplace"/>. Null when it does not exist.</summary>
    Task<FilmVersion?> GetFilmForUpdate(Guid filmId);

    /// <summary>
    /// Replaces the film only if its stored ETag still equals <paramref name="etag"/>. False when another write got
    /// there first (or the film is gone), so the caller re-reads and re-applies instead of overwriting that write.
    /// </summary>
    Task<bool> TryReplace(Film film, string etag);

    /// <summary>
    /// Film ids whose <c>trailers[].id</c> equals <paramref name="trailerId"/>.
    /// Films are partitioned by their own id, so this reads across partitions.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetFilmIdsByTrailerId(Guid trailerId);

    /// <summary>
    /// Reads the film, applies <paramref name="mutate"/>, and replaces it only while the ETag still matches.
    /// On a 412, reads again and applies <paramref name="mutate"/> to that newer film, so a trailer attached in
    /// between is not overwritten. Null when the film does not exist. <paramref name="mutate"/> may run more than
    /// once and must change only the fields this writer owns; return false to leave the stored film unchanged.
    /// Writers that already hold a film must use this, not <see cref="IRepository{TEntity}.Save"/>.
    /// </summary>
    Task<Film?> Update(Guid id, Func<Film, bool> mutate);
}

/// <summary>A film as read, plus the ETag a <see cref="IFilmRepository.TryReplace"/> must match.</summary>
public sealed record FilmVersion(Film Film, string ETag);
