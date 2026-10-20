using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Identify = RedditPodcastPoster.UrlSubmission.Migration.CatalogueMigrateIdentify;

namespace CatalogueMigrateIdentify;

public sealed record CatalogueAuthorityReport(
    Guid PodcastId,
    string ContentKind,
    string Query,
    int HitCount,
    string? ImdbId,
    int? TmdbId,
    long? TvdbId,
    IReadOnlyList<string> HitLabels,
    bool LookupFailed,
    CatalogueAuthorityResolution Resolution,
    IReadOnlyList<Guid>? EpisodeIds = null);

public sealed record CatalogueMigrateIdentifyRunResult(
    int ExitCode,
    int CandidateCount,
    IReadOnlyList<CatalogueAuthorityReport> Authority,
    bool AuthorityLookupSkipped = false);

public class CatalogueMigrateIdentifyProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ICatalogueAuthorityPreviewer authorityPreviewer,
    ILogger<CatalogueMigrateIdentifyProcessor> logger)
{
    /// <summary>
    /// Signal-identified TV and film rows may be checked. A corpus-sized set must not be sent.
    /// The same limit applies to programmes found on one channel.
    /// </summary>
    public const int MaxSignalAuthorityLookups = 50;

    public async Task<CatalogueMigrateIdentifyRunResult> Run(CatalogueMigrateIdentifyRequest request)
    {
        if (request.Apply)
        {
            logger.LogError(
                "Catalogue migrate identify --apply is refused (exit 2). This tool does not write Cosmos or upload Search. Exit 2 is intended.");
            return new CatalogueMigrateIdentifyRunResult(2, 0, []);
        }

        logger.LogInformation(
            "Catalogue migrate identify dry-run — no Cosmos writes. TMDB and TheTVDB run only for signal-identified TV and film, and only when that set is at most {Max}.",
            MaxSignalAuthorityLookups);

        var candidateCount = 0;
        var signalMatches = new List<(Podcast Podcast, string ContentKind, List<Episode> Episodes)>();
        await foreach (var podcast in EnumeratePodcasts(request.PodcastId))
        {
            if (podcast.Removed == true)
            {
                continue;
            }

            var episodes = await episodeRepository.GetByPodcastId(podcast.Id)
                .Where(episode => !episode.Removed)
                .ToListAsync();
            var suggestion = Identify.FromPodcast(podcast, episodes);
            if (suggestion.ContentKind == SubmitClassification.Episode)
            {
                continue;
            }

            candidateCount++;
            logger.LogInformation(
                "Catalogue migrate candidate {PodcastId} {ContentKind} allowlist={RequiresAllowlist} curator={RequiresCurator} episodes={EpisodeCount}",
                podcast.Id,
                suggestion.ContentKind,
                suggestion.RequiresAllowlist,
                suggestion.RequiresCurator,
                episodes.Count);

            if (suggestion.ContentKind is SubmitClassification.Film or SubmitClassification.TvShowEpisode)
            {
                signalMatches.Add((podcast, suggestion.ContentKind, episodes));
            }
        }

        if (signalMatches.Count > MaxSignalAuthorityLookups)
        {
            logger.LogError(
                "Catalogue migrate identify found {Count} TV and film candidates, above {Max}. Not calling TMDB or TheTVDB.",
                signalMatches.Count,
                MaxSignalAuthorityLookups);
            return new CatalogueMigrateIdentifyRunResult(0, candidateCount, [], AuthorityLookupSkipped: true);
        }

        var authority = new List<CatalogueAuthorityReport>(signalMatches.Count);
        foreach (var (podcast, contentKind, episodes) in signalMatches)
        {
            authority.AddRange(await PreviewAuthority(podcast, contentKind, episodes));
        }

        logger.LogInformation(
            "Catalogue migrate identify dry-run complete. Candidates={Count} AuthorityLookups={Lookups}",
            candidateCount,
            authority.Count);
        return new CatalogueMigrateIdentifyRunResult(0, candidateCount, authority);
    }

    private async Task<IReadOnlyList<CatalogueAuthorityReport>> PreviewAuthority(
        Podcast podcast,
        string contentKind,
        IReadOnlyList<Episode> episodes)
    {
        var query = contentKind == SubmitClassification.Film
            ? podcast.Name?.Trim() ?? string.Empty
            : CatalogueTvShowCanonicalNames.ShowNameFor(podcast.Name);
        CatalogueAuthorityPreview preview;
        IReadOnlyList<Guid>? episodeIds = null;
        try
        {
            preview = contentKind == SubmitClassification.Film
                ? await authorityPreviewer.PreviewFilmAsync(query)
                : await authorityPreviewer.PreviewTvShowAsync(query);
            if (contentKind == SubmitClassification.TvShowEpisode
                && preview.Resolution == CatalogueAuthorityResolution.NotInAuthority
                && !preview.LookupFailed)
            {
                var film = await authorityPreviewer.PreviewFilmAsync(query);
                if (film.Resolution == CatalogueAuthorityResolution.Unique)
                {
                    preview = film;
                    contentKind = SubmitClassification.Film;
                }
            }

            if (contentKind == SubmitClassification.TvShowEpisode
                && preview.Resolution != CatalogueAuthorityResolution.Unique
                && !preview.LookupFailed)
            {
                var programmes = CatalogueChannelProgrammes.FromEpisodes(episodes);
                var sameAsPublisher = programmes.Count == 1
                    && programmes[0].Query.Equals(query, StringComparison.OrdinalIgnoreCase);
                if (sameAsPublisher)
                {
                    episodeIds = programmes[0].EpisodeIds;
                }

                if (programmes.Count > MaxSignalAuthorityLookups)
                {
                    logger.LogError(
                        "Catalogue migrate identify found {Count} programmes on {PodcastId}, above {Max}. Not calling TMDB or TheTVDB for those programmes.",
                        programmes.Count,
                        podcast.Id,
                        MaxSignalAuthorityLookups);
                }
                else if (programmes.Count > 0 && !sameAsPublisher)
                {
                    var programmeReports = new List<CatalogueAuthorityReport>(programmes.Count);
                    foreach (var programme in programmes)
                    {
                        programmeReports.Add(await PreviewProgramme(podcast, programme));
                    }

                    return programmeReports;
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Catalogue migrate authority lookup failed for {PodcastId} query={Query}",
                podcast.Id,
                query);
            preview = new CatalogueAuthorityPreview(
                query,
                0,
                null,
                null,
                null,
                [],
                LookupFailed: true,
                Resolution: CatalogueAuthorityResolution.NotInAuthority);
        }

        return [Report(podcast, contentKind, preview, episodeIds)];
    }

    private async Task<CatalogueAuthorityReport> PreviewProgramme(
        Podcast podcast,
        CatalogueChannelProgrammes.Programme programme)
    {
        var query = programme.Query;
        var contentKind = SubmitClassification.TvShowEpisode;
        CatalogueAuthorityPreview preview;
        try
        {
            preview = await authorityPreviewer.PreviewTvShowAsync(query);
            if (preview.Resolution == CatalogueAuthorityResolution.NotInAuthority
                && !preview.LookupFailed)
            {
                var film = await authorityPreviewer.PreviewFilmAsync(query);
                if (film.Resolution == CatalogueAuthorityResolution.Unique)
                {
                    preview = film;
                    contentKind = SubmitClassification.Film;
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Catalogue migrate authority lookup failed for {PodcastId} query={Query}",
                podcast.Id,
                query);
            preview = new CatalogueAuthorityPreview(
                query,
                0,
                null,
                null,
                null,
                [],
                LookupFailed: true,
                Resolution: CatalogueAuthorityResolution.NotInAuthority);
        }

        return Report(podcast, contentKind, preview, programme.EpisodeIds);
    }

    private CatalogueAuthorityReport Report(
        Podcast podcast,
        string contentKind,
        CatalogueAuthorityPreview preview,
        IReadOnlyList<Guid>? episodeIds)
    {
        logger.LogInformation(
            "Catalogue migrate authority {PodcastId} {PodcastName} {ContentKind} query={Query} resolution={Resolution} hits={HitCount} imdbId={ImdbId} tmdbId={TmdbId} tvdbId={TvdbId} samples={Samples} failed={LookupFailed} episodes={EpisodeCount}",
            podcast.Id,
            podcast.Name,
            contentKind,
            preview.Query,
            preview.Resolution,
            preview.HitCount,
            preview.ImdbId,
            preview.TmdbId,
            preview.TvdbId,
            string.Join(" | ", preview.HitLabels),
            preview.LookupFailed,
            episodeIds?.Count ?? 0);
        return new CatalogueAuthorityReport(
            podcast.Id,
            contentKind,
            preview.Query,
            preview.HitCount,
            preview.ImdbId,
            preview.TmdbId,
            preview.TvdbId,
            preview.HitLabels,
            preview.LookupFailed,
            preview.Resolution,
            episodeIds);
    }

    private IAsyncEnumerable<Podcast> EnumeratePodcasts(Guid? podcastId)
    {
        if (podcastId is not { } id)
        {
            return podcastRepository.GetAll();
        }

        return EnumerateOne(id);
    }

    private async IAsyncEnumerable<Podcast> EnumerateOne(Guid id)
    {
        var podcast = await podcastRepository.GetPodcast(id);
        if (podcast is not null)
        {
            yield return podcast;
        }
    }
}
