using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;

namespace RedditPodcastPoster.Catalogue.Authority;

public sealed class CatalogueIdentityEnricher(
    ICatalogueAuthorityPreviewer previewer,
    ICatalogueAuthorityLookup lookup,
    ITheTvdbClient theTvdb,
    ITmdbClient tmdb,
    ILogger<CatalogueIdentityEnricher> logger) : ICatalogueIdentityEnricher
{
    /// <summary>Same floor as <c>CatalogueMatchScorer.DurationBandFloor</c> (five minutes).</summary>
    private static readonly TimeSpan DurationBandFloor = TimeSpan.FromMinutes(5);

    /// <summary>Same proportion as <c>CatalogueMatchScorer.DurationBandProportionOfShorter</c>.</summary>
    private const double DurationBandProportionOfShorter = 0.10;

    public async Task<CatalogueIdentityAdmission> ResolveFilmAsync(
        string title,
        int? year,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default,
        TimeSpan? length = null)
    {
        if (choice == CatalogueIdentityChoice.Cancel)
        {
            return CatalogueIdentityAdmission.NeedsPrompt(
                new CatalogueIdentityHold(CatalogueAuthorityCandidate.FilmKind, title?.Trim() ?? string.Empty, []));
        }

        if (choice == CatalogueIdentityChoice.Continue)
        {
            logger.LogInformation(
                "Catalogue identity film skipped by curator continue. query={Query}",
                title);
            return CatalogueIdentityAdmission.Proceed();
        }

        if (choice == CatalogueIdentityChoice.Select)
        {
            return await SelectFilmAsync(title, selectedKey, cancellationToken).ConfigureAwait(false);
        }

        CatalogueAuthorityPreview preview;
        try
        {
            preview = await previewer.PreviewFilmAsync(title, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity film lookup failed. query={Query}",
                title);
            return CatalogueIdentityAdmission.Proceed();
        }

        return await FromPreviewAsync(
                CatalogueAuthorityCandidate.FilmKind,
                title,
                year,
                length,
                confirmBeforeApply: false,
                preview,
                CollectFilmFromCandidate,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CatalogueIdentityAdmission> LookupFilmAsync(
        string title,
        int? year,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        if (choice == CatalogueIdentityChoice.Cancel)
        {
            return CatalogueIdentityAdmission.NeedsPrompt(
                new CatalogueIdentityHold(CatalogueAuthorityCandidate.FilmKind, title?.Trim() ?? string.Empty, []));
        }

        if (choice == CatalogueIdentityChoice.Continue)
        {
            logger.LogInformation(
                "Catalogue identity film lookup left unchanged by curator. query={Query}",
                title);
            return CatalogueIdentityAdmission.Proceed();
        }

        if (choice == CatalogueIdentityChoice.Select)
        {
            return await SelectFilmAsync(title, selectedKey, cancellationToken).ConfigureAwait(false);
        }

        CatalogueAuthorityPreview preview;
        try
        {
            preview = await previewer.PreviewFilmAsync(title, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity film lookup failed. query={Query}",
                title);
            return CatalogueIdentityAdmission.Proceed();
        }

        return await FromPreviewAsync(
                CatalogueAuthorityCandidate.FilmKind,
                title,
                year,
                length: null,
                confirmBeforeApply: true,
                preview,
                CollectFilmFromCandidate,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CatalogueIdentityAdmission> ResolveTvShowAsync(
        string name,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        if (choice == CatalogueIdentityChoice.Cancel)
        {
            return CatalogueIdentityAdmission.NeedsPrompt(
                new CatalogueIdentityHold(CatalogueAuthorityCandidate.TvShowKind, name?.Trim() ?? string.Empty, []));
        }

        if (choice == CatalogueIdentityChoice.Continue)
        {
            logger.LogInformation(
                "Catalogue identity TV show skipped by curator continue. query={Query}",
                name);
            return CatalogueIdentityAdmission.Proceed();
        }

        if (choice == CatalogueIdentityChoice.Select)
        {
            return await SelectTvShowAsync(name, selectedKey, cancellationToken).ConfigureAwait(false);
        }

        CatalogueAuthorityPreview preview;
        try
        {
            preview = await previewer.PreviewTvShowAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity TV show lookup failed. query={Query}",
                name);
            return CatalogueIdentityAdmission.Proceed();
        }

        return await TvShowFromPreviewAsync(name, preview, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CatalogueIdentityAdmission> LookupTvShowAsync(
        string name,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        if (choice != CatalogueIdentityChoice.Unset)
        {
            return await ResolveTvShowAsync(name, choice, selectedKey, cancellationToken).ConfigureAwait(false);
        }

        CatalogueAuthorityPreview preview;
        try
        {
            preview = await previewer.PreviewTvShowAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity TV show lookup failed. query={Query}",
                name);
            return CatalogueIdentityAdmission.Proceed();
        }

        if (preview.LookupFailed ||
            preview.Resolution == CatalogueAuthorityResolution.Unique ||
            preview.Candidates is { Count: > 0 })
        {
            return await TvShowFromPreviewAsync(name, preview, cancellationToken).ConfigureAwait(false);
        }

        // #1387: TMDB TV and TheTVDB offered nothing to pick. The catalogue may hold as a TV show what IMDb lists
        // as a film or documentary ("3 Days in Dimona"), so offer those instead of a dead-end no-match.
        CatalogueAuthorityPreview otherKinds;
        try
        {
            otherKinds = await previewer.PreviewTvShowOtherKindsAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity TV show film/documentary lookup failed. query={Query}",
                name);
            return await TvShowFromPreviewAsync(name, preview, cancellationToken).ConfigureAwait(false);
        }

        if (otherKinds.Candidates is not { Count: > 0 })
        {
            return await TvShowFromPreviewAsync(name, preview, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Catalogue identity TV show has no TV match; offering film/documentary candidates. query={Query} candidates={Count}",
            name,
            otherKinds.Candidates.Count);
        return await TvShowFromPreviewAsync(name, otherKinds, cancellationToken).ConfigureAwait(false);
    }

    private Task<CatalogueIdentityAdmission> TvShowFromPreviewAsync(
        string name,
        CatalogueAuthorityPreview preview,
        CancellationToken cancellationToken) =>
        FromPreviewAsync(
                CatalogueAuthorityCandidate.TvShowKind,
                name,
                year: null,
                length: null,
                confirmBeforeApply: false,
                preview,
                CollectTvShowFromCandidate,
                cancellationToken);

    public async Task<CatalogueIdentityAdmission> ResolveTvShowEpisodeAsync(
        TvShow show,
        string episodeTitle,
        CatalogueIdentityChoice choice,
        string? selectedKey,
        CancellationToken cancellationToken = default)
    {
        CatalogueAuthorityIds? parentIds = null;
        if (NeedsIdentity(show))
        {
            var showAdmission = await ResolveTvShowAsync(
                    show.Name,
                    choice,
                    selectedKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (showAdmission.Hold)
            {
                return showAdmission;
            }

            parentIds = showAdmission.Ids;
            if (parentIds is not null)
            {
                ApplyMissing(show, parentIds);
            }

            // A show-level select/continue was the answer; do not re-ask for the episode on this pass.
            if (choice is CatalogueIdentityChoice.Select or CatalogueIdentityChoice.Continue)
            {
                return CatalogueIdentityAdmission.Proceed(parentIds: parentIds);
            }
        }
        else if (choice == CatalogueIdentityChoice.Cancel)
        {
            return CatalogueIdentityAdmission.NeedsPrompt(
                new CatalogueIdentityHold(
                    CatalogueAuthorityCandidate.TvShowEpisodeKind,
                    episodeTitle?.Trim() ?? string.Empty,
                    []));
        }
        else if (choice == CatalogueIdentityChoice.Continue)
        {
            logger.LogInformation(
                "Catalogue identity TV episode skipped by curator continue. query={Query} show={Show}",
                episodeTitle,
                show.Name);
            return CatalogueIdentityAdmission.Proceed();
        }
        else if (choice == CatalogueIdentityChoice.Select)
        {
            return await SelectEpisodeAsync(show, episodeTitle, selectedKey, cancellationToken)
                .ConfigureAwait(false);
        }

        if (show.TvdbId is not > 0)
        {
            logger.LogInformation(
                "Catalogue identity TV episode skipped; show has no TheTVDB id. show={Show} episode={Episode}",
                show.Name,
                episodeTitle);
            return CatalogueIdentityAdmission.Proceed(parentIds: parentIds);
        }

        IReadOnlyList<TheTvdbEpisode> episodes;
        try
        {
            episodes = await theTvdb.GetEpisodesAsync(show.TvdbId.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity TV episode list failed. show={Show} tvdbId={TvdbId}",
                show.Name,
                show.TvdbId);
            return CatalogueIdentityAdmission.Proceed(parentIds: parentIds);
        }

        var matches = episodes
            .Where(episode => TitlesAgree(episodeTitle, episode.Name))
            .Take(CatalogueAuthorityPreviewer.SampleLimit)
            .ToList();
        if (matches.Count == 0)
        {
            logger.LogInformation(
                "Catalogue identity TV episode no title match. show={Show} episode={Episode}",
                show.Name,
                episodeTitle);
            return CatalogueIdentityAdmission.Proceed(parentIds: parentIds);
        }

        if (matches.Count > 1)
        {
            var candidates = matches.Select(EpisodeCandidate).ToList();
            logger.LogInformation(
                "Catalogue identity TV episode ambiguous. show={Show} episode={Episode} candidates={Count}",
                show.Name,
                episodeTitle,
                candidates.Count);
            return CatalogueIdentityAdmission.NeedsPrompt(
                new CatalogueIdentityHold(
                    CatalogueAuthorityCandidate.TvShowEpisodeKind,
                    episodeTitle.Trim(),
                    candidates));
        }

        var ids = await lookup.CollectTvShowEpisodeAsync(
                show.TmdbId,
                matches[0].SeasonNumber,
                matches[0].EpisodeNumber,
                matches[0].Id,
                cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Catalogue identity TV episode unique. show={Show} episode={Episode} imdbId={ImdbId} tmdbId={TmdbId} tvdbId={TvdbId}",
            show.Name,
            episodeTitle,
            ids.ImdbId,
            ids.TmdbId,
            ids.TvdbId);
        return CatalogueIdentityAdmission.Proceed(ids, parentIds);
    }

    public bool ApplyMissing(Film film, CatalogueAuthorityIds? ids) =>
        ids?.FillMissing(film) ?? false;

    public bool ApplyMissing(ITvCanonical document, CatalogueAuthorityIds? ids) =>
        ids?.FillMissing(document) ?? false;

    private async Task<CatalogueIdentityAdmission> FromPreviewAsync(
        string target,
        string query,
        int? year,
        TimeSpan? length,
        bool confirmBeforeApply,
        CatalogueAuthorityPreview preview,
        Func<CatalogueAuthorityCandidate, CancellationToken, Task<CatalogueAuthorityIds>> collect,
        CancellationToken cancellationToken)
    {
        if (preview.LookupFailed)
        {
            logger.LogWarning(
                "Catalogue identity lookup failed for {Target}. query={Query}",
                target,
                query);
            return CatalogueIdentityAdmission.Proceed();
        }

        if (preview.Resolution == CatalogueAuthorityResolution.Unique)
        {
            if (confirmBeforeApply && target == CatalogueAuthorityCandidate.FilmKind)
            {
                var uniqueCandidate = ToUniqueFilmCandidate(query, preview);
                if (uniqueCandidate is not null)
                {
                    logger.LogInformation(
                        "Catalogue identity film unique offered for confirmation. query={Query} imdbId={ImdbId} tmdbId={TmdbId}",
                        query,
                        preview.ImdbId,
                        preview.TmdbId);
                    return CatalogueIdentityAdmission.NeedsPrompt(
                        new CatalogueIdentityHold(target, query.Trim(), [uniqueCandidate]));
                }
            }

            var ids = new CatalogueAuthorityIds(
                preview.ImdbId,
                preview.TmdbId,
                preview.TvdbId);
            logger.LogInformation(
                "Catalogue identity {Target} unique. query={Query} imdbId={ImdbId} tmdbId={TmdbId} tvdbId={TvdbId}",
                target,
                query,
                ids.ImdbId,
                ids.TmdbId,
                ids.TvdbId);
            return CatalogueIdentityAdmission.Proceed(ids);
        }

        if (preview.Resolution == CatalogueAuthorityResolution.SeveralProgrammes &&
            preview.Candidates is { Count: > 0 } candidates)
        {
            if (!confirmBeforeApply && year is int knownYear)
            {
                var yearMatches = candidates.Where(c => c.Year == knownYear).ToList();
                if (yearMatches.Count == 1)
                {
                    var yearMatch = yearMatches[0];
                    if (target == CatalogueAuthorityCandidate.FilmKind && yearMatch.TmdbId is not > 0)
                    {
                        logger.LogWarning(
                            "Catalogue identity film year match lacks TMDB id; re-prompting. query={Query} year={Year} key={Key}",
                            query,
                            knownYear,
                            yearMatch.Key);
                        return CatalogueIdentityAdmission.NeedsPrompt(
                            new CatalogueIdentityHold(target, query.Trim(), candidates));
                    }

                    if (target == CatalogueAuthorityCandidate.FilmKind &&
                        yearMatch.TmdbId is int yearTmdbId &&
                        !await FilmRuntimeAgreesAsync(length, yearTmdbId, cancellationToken).ConfigureAwait(false))
                    {
                        logger.LogInformation(
                            "Catalogue identity film year match runtime disagrees; offering picker. query={Query} year={Year} key={Key}",
                            query,
                            knownYear,
                            yearMatch.Key);
                        return CatalogueIdentityAdmission.NeedsPrompt(
                            new CatalogueIdentityHold(target, query.Trim(), candidates));
                    }

                    var ids = await collect(yearMatch, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation(
                        "Catalogue identity {Target} unique by year. query={Query} year={Year} key={Key}",
                        target,
                        query,
                        knownYear,
                        yearMatch.Key);
                    return CatalogueIdentityAdmission.Proceed(ids);
                }
            }

            logger.LogInformation(
                "Catalogue identity {Target} ambiguous. query={Query} candidates={Count}",
                target,
                query,
                candidates.Count);
            return CatalogueIdentityAdmission.NeedsPrompt(
                new CatalogueIdentityHold(target, query.Trim(), candidates));
        }

        // A different title (original-language or IMDb alternative title) is low confidence:
        // offer it for the curator to accept, never apply it, not even on a year match.
        if (preview.Resolution == CatalogueAuthorityResolution.DifferentTitle &&
            preview.Candidates is { Count: > 0 } lowConfidence)
        {
            logger.LogInformation(
                "Catalogue identity {Target} low-confidence candidates offered. query={Query} candidates={Count}",
                target,
                query,
                lowConfidence.Count);
            return CatalogueIdentityAdmission.NeedsPrompt(
                new CatalogueIdentityHold(target, query.Trim(), lowConfidence));
        }

        logger.LogInformation(
            "Catalogue identity {Target} no confident match. query={Query} resolution={Resolution}",
            target,
            query,
            preview.Resolution);
        return CatalogueIdentityAdmission.Proceed();
    }

    private async Task<CatalogueIdentityAdmission> SelectFilmAsync(
        string title,
        string? selectedKey,
        CancellationToken cancellationToken)
    {
        if (CatalogueAuthorityCandidate.TryParseImdbKey(selectedKey, out var imdbId))
        {
            logger.LogInformation(
                "Catalogue identity film selected from IMDb only. query={Query} key={Key} imdbId={ImdbId}",
                title,
                selectedKey,
                imdbId);
            return CatalogueIdentityAdmission.Proceed(new CatalogueAuthorityIds(imdbId, null, null));
        }

        if (!CatalogueAuthorityCandidate.TryParseKey(selectedKey, out var tmdbId, out _) || tmdbId is null)
        {
            logger.LogWarning(
                "Catalogue identity film select missing a TMDB key. query={Query} key={Key}",
                title,
                selectedKey);
            return await ResolveFilmAsync(title, year: null, CatalogueIdentityChoice.Unset, null, cancellationToken)
                .ConfigureAwait(false);
        }

        var ids = await lookup.CollectFilmAsync(tmdbId.Value, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Catalogue identity film selected. query={Query} key={Key} imdbId={ImdbId} tmdbId={TmdbId}",
            title,
            selectedKey,
            ids.ImdbId,
            ids.TmdbId);
        return CatalogueIdentityAdmission.Proceed(ids);
    }

    private async Task<CatalogueIdentityAdmission> SelectTvShowAsync(
        string name,
        string? selectedKey,
        CancellationToken cancellationToken)
    {
        // #1387: a film/documentary (or a series TMDB does not know) offered for a TV show carries only its IMDb id.
        if (CatalogueAuthorityCandidate.TryParseImdbKey(selectedKey, out var imdbId))
        {
            logger.LogInformation(
                "Catalogue identity TV show selected from IMDb only. query={Query} key={Key} imdbId={ImdbId}",
                name,
                selectedKey,
                imdbId);
            return CatalogueIdentityAdmission.Proceed(new CatalogueAuthorityIds(imdbId, null, null));
        }

        if (!CatalogueAuthorityCandidate.TryParseKey(selectedKey, out var tmdbId, out var tvdbId))
        {
            logger.LogWarning(
                "Catalogue identity TV show select missing a key. query={Query} key={Key}",
                name,
                selectedKey);
            return await ResolveTvShowAsync(name, CatalogueIdentityChoice.Unset, null, cancellationToken)
                .ConfigureAwait(false);
        }

        var ids = await lookup.CollectTvShowAsync(tmdbId, tvdbId, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Catalogue identity TV show selected. query={Query} key={Key} imdbId={ImdbId} tmdbId={TmdbId} tvdbId={TvdbId}",
            name,
            selectedKey,
            ids.ImdbId,
            ids.TmdbId,
            ids.TvdbId);
        return CatalogueIdentityAdmission.Proceed(ids);
    }

    private async Task<CatalogueIdentityAdmission> SelectEpisodeAsync(
        TvShow show,
        string episodeTitle,
        string? selectedKey,
        CancellationToken cancellationToken)
    {
        if (!CatalogueAuthorityCandidate.TryParseKey(selectedKey, out _, out var tvdbId) || tvdbId is null)
        {
            logger.LogWarning(
                "Catalogue identity TV episode select missing a TheTVDB key. query={Query} key={Key}",
                episodeTitle,
                selectedKey);
            return await ResolveTvShowEpisodeAsync(
                    show,
                    episodeTitle,
                    CatalogueIdentityChoice.Unset,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var ids = await lookup.CollectTvShowEpisodeAsync(
                show.TmdbId,
                seasonNumber: null,
                episodeNumber: null,
                tvdbId,
                cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Catalogue identity TV episode selected. query={Query} key={Key} imdbId={ImdbId} tmdbId={TmdbId} tvdbId={TvdbId}",
            episodeTitle,
            selectedKey,
            ids.ImdbId,
            ids.TmdbId,
            ids.TvdbId);
        return CatalogueIdentityAdmission.Proceed(ids);
    }

    private Task<CatalogueAuthorityIds> CollectFilmFromCandidate(
        CatalogueAuthorityCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (candidate.TmdbId is not int tmdbId || tmdbId <= 0)
        {
            throw new InvalidOperationException(
                $"Film identity candidate '{candidate.Key}' requires a positive TMDB id.");
        }

        return lookup.CollectFilmAsync(tmdbId, cancellationToken);
    }

    private Task<CatalogueAuthorityIds> CollectTvShowFromCandidate(
        CatalogueAuthorityCandidate candidate,
        CancellationToken cancellationToken) =>
        lookup.CollectTvShowAsync(candidate.TmdbId, candidate.TvdbId, cancellationToken);

    private static CatalogueAuthorityCandidate EpisodeCandidate(TheTvdbEpisode episode)
    {
        var year = int.TryParse(episode.Year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : (int?)null;
        return new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TvdbKey(episode.Id),
            episode.Name,
            year,
            CatalogueAuthorityCandidate.TvShowEpisodeKind,
            CatalogueAuthorityCandidate.TvdbSource,
            episode.TmdbEpisodeId,
            episode.Id,
            episode.CanonicalUrl);
    }

    private static bool NeedsIdentity(ITvCanonical document) =>
        string.IsNullOrWhiteSpace(document.ImdbId) &&
        document.TmdbId is null &&
        document.TvdbId is null;

    /// <summary>Same contiguous-token rule as <see cref="CatalogueAuthorityPreviewer"/>.</summary>
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

    /// <summary>
    /// Edit-time Look up must confirm a unique film before filling blanks. A positive TMDB id uses a
    /// <c>tmdb:</c> key. An IMDb-only unique (null TMDB, as from film fallback) uses <c>imdb:</c> so the
    /// curator still confirms; selecting that key stores the IMDb id alone.
    /// </summary>
    private static CatalogueAuthorityCandidate? ToUniqueFilmCandidate(string query, CatalogueAuthorityPreview preview)
    {
        var title = string.IsNullOrWhiteSpace(preview.HitLabels.FirstOrDefault()) ? query.Trim() : preview.HitLabels[0];
        if (preview.TmdbId is int tmdbId && tmdbId > 0)
        {
            return new CatalogueAuthorityCandidate(
                CatalogueAuthorityCandidate.TmdbKey(tmdbId),
                title,
                Year: null,
                CatalogueAuthorityCandidate.FilmKind,
                CatalogueAuthorityCandidate.TmdbSource,
                tmdbId,
                TvdbId: null);
        }

        var imdbId = preview.ImdbId?.Trim();
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return null;
        }

        return new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.ImdbKey(imdbId),
            title,
            Year: null,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.ImdbSource,
            TmdbId: null,
            TvdbId: null);
    }

    /// <summary>
    /// Same band as <c>DiscoveryKindSuggester.DurationAgrees</c>: when both lengths are known they must sit inside
    /// five minutes or 10% of the shorter; when either side lacks a length, year alone is still enough (RPP #1497).
    /// </summary>
    private async Task<bool> FilmRuntimeAgreesAsync(
        TimeSpan? sourceLength,
        int tmdbId,
        CancellationToken cancellationToken)
    {
        if (sourceLength is not { } length || length <= TimeSpan.Zero)
        {
            return true;
        }

        try
        {
            var metadata = await tmdb.GetMovieMetadataAsync(tmdbId, cancellationToken).ConfigureAwait(false);
            if (metadata?.RuntimeMinutes is not int minutes || minutes <= 0)
            {
                return true;
            }

            return DurationAgrees(length, TimeSpan.FromMinutes(minutes));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Catalogue identity film runtime check failed; treating as agree. tmdbId={TmdbId}",
                tmdbId);
            return true;
        }
    }

    private static bool DurationAgrees(TimeSpan left, TimeSpan right)
    {
        var delta = left > right ? left - right : right - left;
        var shorter = left <= right ? left : right;
        var proportional = TimeSpan.FromTicks((long)(shorter.Ticks * DurationBandProportionOfShorter));
        var band = proportional > DurationBandFloor ? proportional : DurationBandFloor;
        return delta < band;
    }
}
