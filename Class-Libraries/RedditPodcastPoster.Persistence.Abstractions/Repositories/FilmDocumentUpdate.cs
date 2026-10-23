using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// ETag loop for an existing film: read, mutate, replace, and on 412 read again and mutate that newer film.
/// Callers change only the fields they own, so a trailer attached between the attempts is still on the film
/// that gets saved.
/// </summary>
public static class FilmDocumentUpdate
{
    public const int MaxAttempts = 3;

    public static async Task<Film?> Apply(
        Guid id,
        Func<Film, bool> mutate,
        Func<Guid, Task<FilmVersion?>> read,
        Func<Film, string, Task<bool>> tryReplace)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(tryReplace);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var version = await read(id);
            if (version is null)
            {
                return null;
            }

            if (!mutate(version.Film))
            {
                return version.Film;
            }

            if (await tryReplace(version.Film, version.ETag))
            {
                return version.Film;
            }
        }

        throw new InvalidOperationException($"Film '{id}' kept changing; not saved.");
    }
}
