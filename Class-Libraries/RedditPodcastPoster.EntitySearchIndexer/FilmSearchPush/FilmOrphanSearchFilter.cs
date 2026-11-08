using System.Diagnostics.CodeAnalysis;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

/// <summary>
/// Builds the Azure AI Search filter that returns film documents whose ids are not in the live Cosmos
/// film set. Cosmos is the source of truth; search is queried for the set difference so a full push
/// never has to page every film document by unstable <c>$skip</c> on the non-sortable key <c>id</c>.
/// </summary>
public static class FilmOrphanSearchFilter
{
    public const string FilmFilter = $"contentKind eq '{SearchContentKind.Film}'";

    /// <summary>
    /// Azure AI Search rejects OData filters over 128 KB. Stay under that for
    /// <c>contentKind eq 'Film' and not search.in(id, '…', ',')</c>.
    /// </summary>
    public const int MaxFilterLengthChars = 100_000;

    /// <summary>
    /// The only sortable field on <see cref="EpisodeSearchRecord"/> today. Used when orphan candidates
    /// (or a full film-id scan fallback) must be paged: <c>id</c> is the key but <c>IsSortable = false</c>.
    /// Same-release ties can still shuffle across a page boundary; that path is only the oversized-live-set
    /// fallback, not the normal <c>not search.in</c> orphan query.
    /// </summary>
    public const string StablePageOrderBy = "release";

    /// <summary>
    /// When <paramref name="liveFilmIds"/> fits in one filter, returns a filter for film documents whose
    /// <c>id</c> is not among those live ids (every film document when the live set is empty).
    /// Otherwise returns <c>false</c> so the caller can fall back to a paged film-id scan plus set difference.
    /// </summary>
    public static bool TryBuildNotSearchInFilter(
        IReadOnlyCollection<string> liveFilmIds,
        [NotNullWhen(true)] out string? filter)
    {
        ArgumentNullException.ThrowIfNull(liveFilmIds);

        if (liveFilmIds.Count == 0)
        {
            filter = FilmFilter;
            return true;
        }

        foreach (var id in liveFilmIds)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Contains(',') || id.Contains('\''))
            {
                filter = null;
                return false;
            }
        }

        var candidate = $"{FilmFilter} and not search.in(id, '{string.Join(',', liveFilmIds)}', ',')";
        if (candidate.Length > MaxFilterLengthChars)
        {
            filter = null;
            return false;
        }

        filter = candidate;
        return true;
    }
}
