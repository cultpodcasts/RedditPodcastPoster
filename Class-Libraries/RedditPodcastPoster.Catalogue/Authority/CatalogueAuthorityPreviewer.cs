using System.Globalization;
using System.Text;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Catalogue.Authority;

public sealed class CatalogueAuthorityPreviewer(
    ITmdbClient tmdb,
    ITheTvdbClient theTvdb,
    ICatalogueAuthorityLookup lookup) : ICatalogueAuthorityPreviewer
{
    public const int SampleLimit = 8;

    public async Task<CatalogueAuthorityPreview> PreviewFilmAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var original = name?.Trim() ?? string.Empty;
        if (original.Length == 0)
        {
            return Unresolved(original, 0, []);
        }

        CatalogueAuthorityPreview? ambiguousShortening = null;
        foreach (var query in QueryAttempts(original))
        {
            var isOriginal = query.Equals(original, StringComparison.OrdinalIgnoreCase);
            var hits = await tmdb.SearchMoviesAsync(query, cancellationToken).ConfigureAwait(false);
            if (hits.Count > 1)
            {
                var many = Unresolved(
                    query,
                    hits.Count,
                    Labels(hits),
                    FilmCandidates(hits));
                if (isOriginal)
                {
                    return many;
                }

                ambiguousShortening ??= many with { Query = original };
                continue;
            }

            if (hits.Count == 1)
            {
                if (!TitlesAgree(query, hits[0].Name))
                {
                    if (isOriginal)
                    {
                        return Unresolved(query, 1, Labels(hits));
                    }

                    continue;
                }

                var ids = await lookup.CollectFilmAsync(hits[0].Id, cancellationToken).ConfigureAwait(false);
                return Unique(query, ids.ImdbId, ids.TmdbId, tvdbId: null, Labels(hits));
            }
        }

        return ambiguousShortening ?? Unresolved(original, 0, []);
    }

    public async Task<CatalogueAuthorityPreview> PreviewTvShowAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var original = name?.Trim() ?? string.Empty;
        if (original.Length == 0)
        {
            return Unresolved(original, 0, []);
        }

        CatalogueAuthorityPreview? ambiguousShortening = null;
        foreach (var query in QueryAttempts(original))
        {
            var isOriginal = query.Equals(original, StringComparison.OrdinalIgnoreCase);
            var tmdbHits = await tmdb.SearchTvAsync(query, cancellationToken).ConfigureAwait(false);
            if (tmdbHits.Count > 1)
            {
                var many = Unresolved(
                    query,
                    tmdbHits.Count,
                    Labels(tmdbHits),
                    TvShowTmdbCandidates(tmdbHits));
                if (isOriginal)
                {
                    return many;
                }

                ambiguousShortening ??= many with { Query = original };
                continue;
            }

            if (tmdbHits.Count == 1)
            {
                if (!TitlesAgree(query, tmdbHits[0].Name))
                {
                    if (isOriginal)
                    {
                        return Unresolved(query, 1, Labels(tmdbHits));
                    }

                    continue;
                }

                var ids = await lookup.CollectTvShowAsync(tmdbHits[0].Id, tvdbId: null, cancellationToken)
                    .ConfigureAwait(false);
                if (ids.TvdbId is not > 0)
                {
                    var tvdbHits = await SearchSeriesAsync(query, cancellationToken).ConfigureAwait(false);
                    if (tvdbHits.Count == 1 && TitlesAgree(query, tvdbHits[0].Name))
                    {
                        ids = await lookup.CollectTvShowAsync(tmdbHits[0].Id, tvdbHits[0].Id, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                return Unique(query, ids.ImdbId, ids.TmdbId, ids.TvdbId, Labels(tmdbHits));
            }

            var seriesHits = await SearchSeriesAsync(query, cancellationToken).ConfigureAwait(false);
            if (seriesHits.Count > 1)
            {
                var many = Unresolved(
                    query,
                    seriesHits.Count,
                    seriesHits.Take(SampleLimit).Select(Label).ToList(),
                    TvShowTvdbCandidates(seriesHits));
                if (isOriginal)
                {
                    return many;
                }

                ambiguousShortening ??= many with { Query = original };
                continue;
            }

            if (seriesHits.Count == 1)
            {
                if (!TitlesAgree(query, seriesHits[0].Name))
                {
                    if (isOriginal)
                    {
                        return Unresolved(query, 1, [Label(seriesHits[0])]);
                    }

                    continue;
                }

                var ids = await lookup.CollectTvShowAsync(tmdbId: null, seriesHits[0].Id, cancellationToken)
                    .ConfigureAwait(false);
                return Unique(query, ids.ImdbId, ids.TmdbId, ids.TvdbId, [Label(seriesHits[0])]);
            }
        }

        return ambiguousShortening ?? Unresolved(original, 0, []);
    }

    private async Task<IReadOnlyList<TheTvdbSearchHit>> SearchSeriesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var hits = await theTvdb.SearchAsync(query, TheTvdbSearchType.Series, cancellationToken).ConfigureAwait(false);
        return hits.Where(hit => hit.Type == TheTvdbSearchType.Series).ToList();
    }

    /// <summary>
    /// The full title, then a shorter clause only when the full title finds nothing.
    /// Many hits on the full title do not fall through.
    /// Many hits on a shorter clause are not the show: the full title stays the query.
    /// A trailing parenthetical is a channel note. A colon keeps the clause before it.
    /// A " - " keeps the clause after it, because that is the programme rather than the strand.
    /// </summary>
    private static IEnumerable<string> QueryAttempts(string query)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { query, WithoutParenthetical(query), BeforeColon(query), AfterHyphen(query) })
        {
            if (candidate is not { Length: > 0 } || !seen.Add(candidate))
            {
                continue;
            }

            yield return candidate;
        }
    }

    private static string? WithoutParenthetical(string query)
    {
        var paren = query.IndexOf('(');
        if (paren <= 0)
        {
            return null;
        }

        var head = query[..paren].Trim();
        return head.Length == 0 || head.Equals(query, StringComparison.OrdinalIgnoreCase) ? null : head;
    }

    private static string? BeforeColon(string query)
    {
        var colon = query.IndexOf(':');
        if (colon <= 0)
        {
            return null;
        }

        var head = query[..colon].Trim();
        return head.Length == 0 || head.Equals(query, StringComparison.OrdinalIgnoreCase) ? null : head;
    }

    private static string? AfterHyphen(string query)
    {
        const string separator = " - ";
        var hyphen = query.IndexOf(separator, StringComparison.Ordinal);
        if (hyphen <= 0)
        {
            return null;
        }

        var tail = query[(hyphen + separator.Length)..].Trim();
        return tail.Length == 0 || tail.Equals(query, StringComparison.OrdinalIgnoreCase) ? null : tail;
    }

    /// <summary>
    /// A hit counts only when its name and the query share the same words,
    /// or one title's words sit inside the other. "Newsnight" agrees with "BBC Newsnight".
    /// A different spelling or a stub such as "BBC-3" does not.
    /// </summary>
    private static bool TitlesAgree(string query, string hitName)
    {
        var queryTokens = Tokens(query);
        var hitTokens = Tokens(hitName);
        if (queryTokens.Count == 0 || hitTokens.Count == 0)
        {
            return false;
        }

        return ContainsContiguous(queryTokens, hitTokens) || ContainsContiguous(hitTokens, queryTokens);
    }

    private static List<string> Tokens(string value)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
                continue;
            }

            if (current.Length == 0)
            {
                continue;
            }

            tokens.Add(current.ToString());
            current.Clear();
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static bool ContainsContiguous(IReadOnlyList<string> haystack, IReadOnlyList<string> needle)
    {
        if (needle.Count > haystack.Count)
        {
            return false;
        }

        for (var start = 0; start <= haystack.Count - needle.Count; start++)
        {
            var matches = true;
            for (var offset = 0; offset < needle.Count; offset++)
            {
                if (!haystack[start + offset].Equals(needle[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static CatalogueAuthorityPreview Unique(
        string query,
        string? imdbId,
        int? tmdbId,
        long? tvdbId,
        IReadOnlyList<string> labels) =>
        new(
            query,
            HitCount: 1,
            imdbId,
            tmdbId,
            tvdbId,
            labels,
            Resolution: CatalogueAuthorityResolution.Unique);

    private static CatalogueAuthorityPreview Unresolved(
        string query,
        int hitCount,
        IReadOnlyList<string> labels,
        IReadOnlyList<CatalogueAuthorityCandidate>? candidates = null) =>
        new(
            query,
            hitCount,
            null,
            null,
            null,
            labels,
            Resolution: hitCount switch
            {
                0 => CatalogueAuthorityResolution.NotInAuthority,
                1 => CatalogueAuthorityResolution.DifferentTitle,
                _ => CatalogueAuthorityResolution.SeveralProgrammes
            },
            Candidates: hitCount > 1 ? candidates : null);

    private static List<string> Labels(IReadOnlyList<TmdbSearchHit> hits) =>
        hits.Take(SampleLimit).Select(Label).ToList();

    private static List<CatalogueAuthorityCandidate> FilmCandidates(IReadOnlyList<TmdbSearchHit> hits) =>
        hits.Take(SampleLimit).Select(hit => new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TmdbKey(hit.Id),
            hit.Name,
            hit.Year,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.TmdbSource,
            hit.Id,
            TvdbId: null)).ToList();

    private static List<CatalogueAuthorityCandidate> TvShowTmdbCandidates(IReadOnlyList<TmdbSearchHit> hits) =>
        hits.Take(SampleLimit).Select(hit => new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TmdbKey(hit.Id),
            hit.Name,
            hit.Year,
            CatalogueAuthorityCandidate.TvShowKind,
            CatalogueAuthorityCandidate.TmdbSource,
            hit.Id,
            TvdbId: null)).ToList();

    private static List<CatalogueAuthorityCandidate> TvShowTvdbCandidates(IReadOnlyList<TheTvdbSearchHit> hits) =>
        hits.Take(SampleLimit).Select(hit => new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TvdbKey(hit.Id),
            hit.Name,
            ParseYear(hit.Year),
            CatalogueAuthorityCandidate.TvShowKind,
            CatalogueAuthorityCandidate.TvdbSource,
            TmdbId: null,
            hit.Id,
            hit.CanonicalUrl)).ToList();

    private static int? ParseYear(string? year) =>
        int.TryParse(year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string Label(TmdbSearchHit hit) =>
        hit.Year is { } year
            ? $"{hit.Name} ({year.ToString(CultureInfo.InvariantCulture)})"
            : hit.Name;

    private static string Label(TheTvdbSearchHit hit) =>
        string.IsNullOrWhiteSpace(hit.Year) ? hit.Name : $"{hit.Name} ({hit.Year})";
}
