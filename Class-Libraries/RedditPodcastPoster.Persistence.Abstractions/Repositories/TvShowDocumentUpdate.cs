using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// ETag loop for an existing TV show: read, mutate, replace, and on 412 read again and mutate that newer show.
/// Callers change only the fields they own, so a trailer attached between the attempts is still on the show
/// that gets saved (RPP #1412). The TV twin of <see cref="FilmDocumentUpdate"/>.
/// </summary>
public static class TvShowDocumentUpdate
{
    public const int MaxAttempts = 3;

    public static async Task<TvShow?> Apply(
        Guid id,
        Func<TvShow, bool> mutate,
        Func<Guid, Task<TvShowVersion?>> read,
        Func<TvShow, string, Task<bool>> tryReplace)
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

            if (!mutate(version.TvShow))
            {
                return version.TvShow;
            }

            if (await tryReplace(version.TvShow, version.ETag))
            {
                return version.TvShow;
            }
        }

        throw new InvalidOperationException($"TV show '{id}' kept changing; not saved.");
    }
}
