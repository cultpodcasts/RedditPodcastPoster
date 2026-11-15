using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority.Imdb;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.Catalogue.Authority;

public sealed class CatalogueAuthorityPreviewer(
    ITmdbClient tmdb,
    ITheTvdbClient theTvdb,
    ICatalogueAuthorityLookup lookup,
    IImdbSuggestionClient imdb,
    ILogger<CatalogueAuthorityPreviewer> logger) : ICatalogueAuthorityPreviewer
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
                if (!TitlesAgree(query, hits[0]))
                {
                    if (isOriginal)
                    {
                        return await FilmFallbackAsync(query, FilmCandidates(hits), cancellationToken)
                            .ConfigureAwait(false);
                    }

                    continue;
                }

                var ids = await lookup.CollectFilmAsync(hits[0].Id, cancellationToken).ConfigureAwait(false);
                return Unique(query, ids.ImdbId, ids.TmdbId, tvdbId: null, Labels(hits));
            }
        }

        return ambiguousShortening ??
               await FilmFallbackAsync(original, [], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// TMDB found nothing, or one film under a different title. IMDb's suggestion feed matches
    /// alternative titles, so it is asked next. Each IMDb film is resolved to its TMDB record
    /// when TMDB has one, so a pick stores both ids.
    /// One IMDb film whose whole title equals the query (IMDb title, or the TMDB listed or original
    /// title after <c>find</c>), and no TMDB hit, is the film. Containment is not enough here:
    /// "The Last Stand of Waco" must not become "Waco".
    /// Anything else is offered to the curator as low-confidence candidates
    /// (<see cref="CatalogueAuthorityResolution.DifferentTitle"/>), never applied unasked.
    /// A failed TMDB <c>find</c> keeps the IMDb film as an IMDb-only candidate and nothing is applied,
    /// so one throttled call cannot lose the candidates already found.
    /// </summary>
    private async Task<CatalogueAuthorityPreview> FilmFallbackAsync(
        string query,
        IReadOnlyList<CatalogueAuthorityCandidate> tmdbCandidates,
        CancellationToken cancellationToken)
    {
        var suggestions = await imdb.SuggestAsync(query, cancellationToken).ConfigureAwait(false) ?? [];
        var candidates = tmdbCandidates.ToList();
        var exact = new List<(CatalogueAuthorityCandidate Candidate, string ImdbId)>();
        var findFailed = false;
        foreach (var suggestion in PlausibleFilms(query, suggestions))
        {
            TmdbSearchHit? tmdbHit = null;
            try
            {
                tmdbHit = await tmdb.FindMovieByImdbIdAsync(suggestion.ImdbId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException ||
                                              !cancellationToken.IsCancellationRequested)
            {
                findFailed = true;
                logger.LogWarning(
                    exception,
                    "TMDB find by IMDb id failed; keeping the IMDb film as a candidate. query={Query} imdbId={ImdbId}",
                    query,
                    suggestion.ImdbId);
            }

            var candidate = tmdbHit is null
                ? ImdbFilmCandidate(suggestion)
                : FilmCandidate(tmdbHit);
            var existing = candidates.FirstOrDefault(c => c.Key == candidate.Key);
            if (existing is null)
            {
                candidates.Add(candidate);
            }

            if (TitlesEqual(query, suggestion.Title) || (tmdbHit is not null && TitlesEqual(query, tmdbHit)))
            {
                exact.Add((existing ?? candidate, suggestion.ImdbId));
            }
        }

        if (candidates.Count == 0)
        {
            return Unresolved(query, 0, []);
        }

        if (!findFailed && tmdbCandidates.Count == 0 && candidates.Count == 1 && exact.Count == 1)
        {
            var (only, imdbId) = exact[0];
            if (only.TmdbId is int tmdbId)
            {
                var ids = await lookup.CollectFilmAsync(tmdbId, cancellationToken).ConfigureAwait(false);
                return Unique(query, ids.ImdbId ?? imdbId, ids.TmdbId ?? tmdbId, tvdbId: null, [Label(only)]);
            }

            return Unique(query, imdbId, tmdbId: null, tvdbId: null, [Label(only)]);
        }

        var offered = candidates.Take(SampleLimit).ToList();
        return new CatalogueAuthorityPreview(
            query,
            offered.Count,
            null,
            null,
            null,
            offered.Select(Label).ToList(),
            Resolution: CatalogueAuthorityResolution.DifferentTitle,
            Candidates: offered);
    }

    /// <summary>
    /// IMDb suggestions are fuzzy ("Faderhuset" also returns "The Loud House Movie").
    /// A film counts when its title agrees with the query, or when it is IMDb's top suggestion,
    /// because that is where an alternative-title match lands.
    /// </summary>
    private static IEnumerable<ImdbSuggestion> PlausibleFilms(string query, IReadOnlyList<ImdbSuggestion> suggestions) =>
        suggestions
            .Where((suggestion, index) =>
                suggestion.IsFilm && (index == 0 || TitlesAgree(query, suggestion.Title)))
            .DistinctBy(suggestion => suggestion.ImdbId, StringComparer.OrdinalIgnoreCase)
            .Take(SampleLimit);

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
                if (!TitlesAgree(query, tmdbHits[0]))
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

    /// <summary>
    /// A TMDB hit agrees on its listed title (the usual containment rule), or on its whole
    /// original-language title. A short original title merely contained in the query is weak evidence,
    /// so the original title must match the query word for word.
    /// </summary>
    private static bool TitlesAgree(string query, TmdbSearchHit hit) =>
        TitlesAgree(query, hit.Name) ||
        (hit.OriginalName is { Length: > 0 } originalName && TitlesEqual(query, originalName));

    /// <summary>The listed or original title equals the query word for word.</summary>
    private static bool TitlesEqual(string query, TmdbSearchHit hit) =>
        TitlesEqual(query, hit.Name) ||
        (hit.OriginalName is { Length: > 0 } originalName && TitlesEqual(query, originalName));

    /// <summary>Same words in the same order, ignoring case and punctuation.</summary>
    private static bool TitlesEqual(string query, string title)
    {
        var queryTokens = Tokens(query);
        return queryTokens.Count > 0 && queryTokens.SequenceEqual(Tokens(title), StringComparer.Ordinal);
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
        hits.Take(SampleLimit).Select(FilmCandidate).ToList();

    private static CatalogueAuthorityCandidate FilmCandidate(TmdbSearchHit hit) =>
        new(
            CatalogueAuthorityCandidate.TmdbKey(hit.Id),
            DisplayName(hit),
            hit.Year,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.TmdbSource,
            hit.Id,
            TvdbId: null,
            CatalogueCanonicalId.TmdbMoviePage(hit.Id),
            CatalogueCanonicalId.TmdbPosterThumbnail(hit.PosterPath),
            hit.Overview);

    private static CatalogueAuthorityCandidate ImdbFilmCandidate(ImdbSuggestion suggestion) =>
        new(
            CatalogueAuthorityCandidate.ImdbKey(suggestion.ImdbId),
            suggestion.Title,
            suggestion.Year,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.ImdbSource,
            TmdbId: null,
            TvdbId: null,
            CatalogueCanonicalId.ImdbPage(suggestion.ImdbId),
            suggestion.ImageUrl,
            suggestion.Cast);

    /// <summary>"Father's House / Faderhuset" so the curator sees why a different title was offered.</summary>
    private static string DisplayName(TmdbSearchHit hit) =>
        hit.OriginalName is { Length: > 0 } originalName ? $"{hit.Name} / {originalName}" : hit.Name;

    private static List<CatalogueAuthorityCandidate> TvShowTmdbCandidates(IReadOnlyList<TmdbSearchHit> hits) =>
        hits.Take(SampleLimit).Select(hit => new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TmdbKey(hit.Id),
            DisplayName(hit),
            hit.Year,
            CatalogueAuthorityCandidate.TvShowKind,
            CatalogueAuthorityCandidate.TmdbSource,
            hit.Id,
            TvdbId: null,
            CatalogueCanonicalId.TmdbTvPage(hit.Id),
            CatalogueCanonicalId.TmdbPosterThumbnail(hit.PosterPath),
            hit.Overview)).ToList();

    private static List<CatalogueAuthorityCandidate> TvShowTvdbCandidates(IReadOnlyList<TheTvdbSearchHit> hits) =>
        hits.Take(SampleLimit).Select(hit => new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TvdbKey(hit.Id),
            hit.Name,
            ParseYear(hit.Year),
            CatalogueAuthorityCandidate.TvShowKind,
            CatalogueAuthorityCandidate.TvdbSource,
            TmdbId: null,
            hit.Id,
            hit.ImdbUrl ?? hit.CanonicalUrl,
            ThumbnailUrl: null,
            Description: null)).ToList();

    private static int? ParseYear(string? year) =>
        int.TryParse(year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string Label(TmdbSearchHit hit) =>
        hit.Year is { } year
            ? $"{hit.Name} ({year.ToString(CultureInfo.InvariantCulture)})"
            : hit.Name;

    private static string Label(CatalogueAuthorityCandidate candidate) =>
        candidate.Year is { } year
            ? $"{candidate.Title} ({year.ToString(CultureInfo.InvariantCulture)})"
            : candidate.Title;

    private static string Label(TheTvdbSearchHit hit) =>
        string.IsNullOrWhiteSpace(hit.Year) ? hit.Name : $"{hit.Name} ({hit.Year})";
}
