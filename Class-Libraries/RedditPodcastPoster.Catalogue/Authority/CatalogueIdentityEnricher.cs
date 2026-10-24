using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;

namespace RedditPodcastPoster.Catalogue.Authority;

public sealed class CatalogueIdentityEnricher(
    ICatalogueAuthorityPreviewer previewer,
    ICatalogueAuthorityLookup lookup,
    ITheTvdbClient theTvdb,
    ILogger<CatalogueIdentityEnricher> logger) : ICatalogueIdentityEnricher
{
    public async Task<CatalogueIdentityAdmission> ResolveFilmAsync(
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

        return await FromPreviewAsync(
                CatalogueAuthorityCandidate.TvShowKind,
                name,
                year: null,
                preview,
                CollectTvShowFromCandidate,
                cancellationToken)
            .ConfigureAwait(false);
    }

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
            if (year is int knownYear)
            {
                var yearMatches = candidates.Where(c => c.Year == knownYear).ToList();
                if (yearMatches.Count == 1)
                {
                    var ids = await collect(yearMatches[0], cancellationToken).ConfigureAwait(false);
                    logger.LogInformation(
                        "Catalogue identity {Target} unique by year. query={Query} year={Year} key={Key}",
                        target,
                        query,
                        knownYear,
                        yearMatches[0].Key);
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
        CancellationToken cancellationToken) =>
        lookup.CollectFilmAsync(candidate.TmdbId ?? 0, cancellationToken);

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
}
