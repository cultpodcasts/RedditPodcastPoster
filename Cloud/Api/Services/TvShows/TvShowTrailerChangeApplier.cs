using Api.Models;
using RedditPodcastPoster.Models.TvShows;

namespace Api.Services.TvShows;

/// <summary>
/// The TV show PATCH <c>trailers</c> keep-list (RPP #1412), the same rules as a film's: the list is the trailers to
/// keep, in display order, each with an optional new title. An id listed twice or one the show does not have is
/// invalid. <see cref="TryValidate"/> runs before anything is applied, so a 400 leaves the show untouched.
/// </summary>
public static class TvShowTrailerChangeApplier
{
    public static bool TryValidate(TvShow show, TvShowTrailerChange[]? keep, out string? error)
    {
        error = null;
        if (keep is null)
        {
            return true;
        }

        var seen = new HashSet<Guid>();
        foreach (var trailer in keep)
        {
            if (!seen.Add(trailer.Id))
            {
                error = $"trailer '{trailer.Id}' is listed twice";
                return false;
            }

            if (!show.HasTrailer(trailer.Id))
            {
                error = $"the TV show has no trailer '{trailer.Id}'";
                return false;
            }
        }

        return true;
    }

    /// <summary>Keeps exactly the listed trailers in the listed order. Call only after <see cref="TryValidate"/>.</summary>
    public static void Apply(TvShow show, TvShowTrailerChange[] keep)
    {
        var current = show.Trailers ?? [];
        var kept = new List<TvShowTrailer>(keep.Length);
        foreach (var change in keep)
        {
            var trailer = current.First(x => x.Id == change.Id);
            if (!string.IsNullOrWhiteSpace(change.Title))
            {
                trailer.Title = change.Title.Trim();
            }

            kept.Add(trailer);
        }

        show.Trailers = kept.Count == 0 ? null : kept;
    }

    /// <summary>Stored trailer ids the client did not list as expected (null and empty both mean none were listed).</summary>
    public static Guid[] TrailerIdsTheClientDidNotList(TvShow show, Guid[]? expectedTrailerIds)
    {
        var expected = expectedTrailerIds is null ? [] : expectedTrailerIds.ToHashSet();
        return show.Trailers?
                   .Where(trailer => !expected.Contains(trailer.Id))
                   .Select(trailer => trailer.Id)
                   .ToArray()
               ?? [];
    }
}
