using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Option B for #1200: suggest a kind at discovery accept, never auto-apply. Known-channel auto-routing
/// is left alone (a mapped news organisation or TV show returns no hint).
/// TMDB film hints require both a title agreement and a runtime inside
/// <see cref="CatalogueMatchScorer.GetDurationBand"/>. Catalogue matches use the same title predicate;
/// films and news reports also use the duration band when both sides have a length.
/// </summary>
public sealed class DiscoveryKindSuggester(
    ICatalogueParentLookup parentLookup,
    IFilmRepository films,
    ITvShowRepository tvShows,
    INewsOrganisationRepository newsOrganisations,
    INewsReportRepository newsReports,
    ITmdbClient tmdb,
    ILogger<DiscoveryKindSuggester> logger) : IDiscoveryKindSuggester
{
    public const int TmdbCandidateLimit = 8;

    private CatalogueCache? _cache;
    private readonly SemaphoreSlim _cacheGate = new(1, 1);

    public async Task<DiscoveryKindHold?> SuggestAsync(
        DiscoveryResult discoveryResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discoveryResult);

        if (await IsMappedToParent(discoveryResult, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var query = TitleQuery(discoveryResult.EpisodeName) ?? TitleQuery(discoveryResult.ShowName);
        if (query is null)
        {
            return null;
        }

        var cache = await LoadCache(cancellationToken).ConfigureAwait(false);
        var candidates = new List<CatalogueAuthorityCandidate>();
        candidates.AddRange(MatchFilms(query, discoveryResult.Length, cache.Films));
        candidates.AddRange(MatchTvShows(query, discoveryResult.ShowName, cache.TvShows));
        candidates.AddRange(MatchNewsReports(query, discoveryResult.Length, cache.NewsReports));

        if (discoveryResult.Length is { } duration && duration > TimeSpan.Zero)
        {
            candidates.AddRange(
                await MatchTmdbFilms(query, duration, cancellationToken).ConfigureAwait(false));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var distinct = candidates
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var target = TargetLabel(distinct);
        logger.LogInformation(
            "Discovery-result '{DiscoveryResultId}' has {Count} kind suggestion(s) for {Target} '{Query}'.",
            discoveryResult.Id,
            distinct.Count,
            target,
            query);
        return new DiscoveryKindHold(target, query, distinct, discoveryResult.Id);
    }

    private async Task<bool> IsMappedToParent(DiscoveryResult discoveryResult, CancellationToken cancellationToken)
    {
        var ids = new EpisodeUrlOwnerIds(
            discoveryResult.SpotifyId,
            discoveryResult.AppleId,
            discoveryResult.YouTubeChannelId);
        if (ids.IsEmpty)
        {
            return false;
        }

        var parent = await parentLookup.FindByOwner(ids).ConfigureAwait(false);
        return parent != null && !parent.Parent.IsRemoved();
    }

    private async Task<CatalogueCache> LoadCache(CancellationToken cancellationToken)
    {
        if (_cache != null)
        {
            return _cache;
        }

        await _cacheGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache != null)
            {
                return _cache;
            }

            var filmList = new List<Film>();
            await foreach (var film in films.GetAll().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (!film.IsRemoved())
                {
                    filmList.Add(film);
                }
            }

            var showList = new List<TvShow>();
            await foreach (var show in tvShows.GetAll().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (!show.IsRemoved())
                {
                    showList.Add(show);
                }
            }

            var reports = new List<(NewsOrganisation Org, NewsReport Report)>();
            await foreach (var org in newsOrganisations.GetAll().WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
            {
                if (org.IsRemoved())
                {
                    continue;
                }

                await foreach (var report in newsReports.GetByNewsOrganisationId(org.Id)
                                   .WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    if (!report.IsRemoved() && report.ParentRemoved != true)
                    {
                        reports.Add((org, report));
                    }
                }
            }

            _cache = new CatalogueCache(filmList, showList, reports);
            return _cache;
        }
        finally
        {
            _cacheGate.Release();
        }
    }

    private static IEnumerable<CatalogueAuthorityCandidate> MatchFilms(
        string query,
        TimeSpan? duration,
        IReadOnlyList<Film> catalogue)
    {
        foreach (var film in catalogue)
        {
            if (!TitlesAgree(query, film.Name) &&
                (film.AlternativeTitles is null || film.AlternativeTitles.TrueForAll(t => !TitlesAgree(query, t))))
            {
                continue;
            }

            if (!DurationAgrees(duration, film.Length))
            {
                continue;
            }

            yield return new CatalogueAuthorityCandidate(
                DiscoveryKindKeys.Film(film.Id),
                film.Name,
                YearOf(film.Release),
                CatalogueAuthorityCandidate.FilmKind,
                DiscoveryKindKeys.CatalogueSource,
                film.TmdbId,
                null);
        }
    }

    private static IEnumerable<CatalogueAuthorityCandidate> MatchTvShows(
        string query,
        string? showName,
        IReadOnlyList<TvShow> catalogue)
    {
        var showQuery = TitleQuery(showName) ?? query;
        foreach (var show in catalogue)
        {
            if (!TitlesAgree(query, show.Name) && !TitlesAgree(showQuery, show.Name))
            {
                continue;
            }

            yield return new CatalogueAuthorityCandidate(
                DiscoveryKindKeys.TvShow(show.Id),
                show.Name,
                null,
                CatalogueAuthorityCandidate.TvShowKind,
                DiscoveryKindKeys.CatalogueSource,
                show.TmdbId,
                show.TvdbId);
        }
    }

    private static IEnumerable<CatalogueAuthorityCandidate> MatchNewsReports(
        string query,
        TimeSpan? duration,
        IReadOnlyList<(NewsOrganisation Org, NewsReport Report)> catalogue)
    {
        foreach (var (org, report) in catalogue)
        {
            if (!TitlesAgree(query, report.Title))
            {
                continue;
            }

            if (!DurationAgrees(duration, report.Length))
            {
                continue;
            }

            yield return new CatalogueAuthorityCandidate(
                DiscoveryKindKeys.NewsReport(org.Id, report.Id),
                report.Title,
                YearOf(report.Release),
                SubmitClassification.NewsReport,
                DiscoveryKindKeys.CatalogueSource,
                null,
                null,
                Description: org.Name);
        }
    }

    private async Task<IReadOnlyList<CatalogueAuthorityCandidate>> MatchTmdbFilms(
        string query,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TmdbSearchHit> hits;
        try
        {
            hits = await tmdb.SearchMoviesAsync(query, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "TMDB movie search failed for discovery kind hint. query={Query}", query);
            return [];
        }

        if (hits is null || hits.Count == 0)
        {
            return [];
        }

        var agreed = hits.Where(hit => TitlesAgree(query, hit.Name) || TitlesAgree(query, hit.OriginalName))
            .Take(TmdbCandidateLimit)
            .ToList();
        if (agreed.Count == 0)
        {
            return [];
        }

        var matched = new List<CatalogueAuthorityCandidate>();
        foreach (var hit in agreed)
        {
            TmdbMovieMetadata? meta;
            try
            {
                meta = await tmdb.GetMovieMetadataAsync(hit.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "TMDB movie metadata failed for discovery kind hint. query={Query} tmdbId={TmdbId}",
                    query,
                    hit.Id);
                continue;
            }

            if (meta?.RuntimeMinutes is not > 0)
            {
                continue;
            }

            var runtime = TimeSpan.FromMinutes(meta.RuntimeMinutes.Value);
            if (!DurationAgrees(duration, runtime))
            {
                continue;
            }

            matched.Add(new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(hit.Id),
                meta.Title,
                meta.ReleaseDate?.Year ?? hit.Year,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                hit.Id,
                null,
                PageUrl: new Uri($"https://www.themoviedb.org/movie/{hit.Id}"),
                ThumbnailUrl: TmdbImage.Poster(hit.PosterPath),
                Description: meta.Overview));
        }

        return matched;
    }

    /// <summary>
    /// When both sides have a length, they must sit inside the catalogue duration band (five minutes, or 10% of
    /// the shorter title). When either side lacks a length, a title match is still enough for a catalogue item
    /// (TV shows have no runtime) but not for a TMDB film, which is only offered when both runtimes are known.
    /// </summary>
    internal static bool DurationAgrees(TimeSpan? left, TimeSpan? right)
    {
        if (left is not { } leftLength || right is not { } rightLength ||
            leftLength <= TimeSpan.Zero || rightLength <= TimeSpan.Zero)
        {
            return true;
        }

        var delta = leftLength > rightLength ? leftLength - rightLength : rightLength - leftLength;
        return delta < CatalogueMatchScorer.GetDurationBand(leftLength, rightLength);
    }

    internal static string? TitleQuery(string? name)
    {
        var normalised = EpisodeTitleMatch.Normalise(name);
        if (normalised.Length == 0)
        {
            return null;
        }

        return StripTrailingYear(normalised);
    }

    internal static bool TitlesAgree(string? query, string? hitName)
    {
        var left = TitleQuery(query);
        var right = TitleQuery(hitName);
        return left != null && right != null && EpisodeTitleMatch.EqualsOrContains(left, right);
    }

    private static string StripTrailingYear(string title)
    {
        if (title.Length < 7 || title[^1] != ')')
        {
            return title;
        }

        var open = title.LastIndexOf(" (", StringComparison.Ordinal);
        if (open < 0 || title.Length - open != 7)
        {
            return title;
        }

        var year = title.AsSpan(open + 2, 4);
        foreach (var ch in year)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return title;
            }
        }

        return title[..open].TrimEnd();
    }

    private static int? YearOf(CatalogueRelease? release) => release is null ? null : release.ToSortUtc().Year;

    private static string TargetLabel(IReadOnlyList<CatalogueAuthorityCandidate> candidates)
    {
        var kinds = candidates.Select(c => c.Kind).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (kinds.Count != 1)
        {
            return "catalogue";
        }

        return kinds[0] switch
        {
            CatalogueAuthorityCandidate.FilmKind => "film",
            CatalogueAuthorityCandidate.TvShowKind => "TV show",
            SubmitClassification.NewsReport => "news report",
            _ => "catalogue"
        };
    }

    private sealed record CatalogueCache(
        IReadOnlyList<Film> Films,
        IReadOnlyList<TvShow> TvShows,
        IReadOnlyList<(NewsOrganisation Org, NewsReport Report)> NewsReports);
}