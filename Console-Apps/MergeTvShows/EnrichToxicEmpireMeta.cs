using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace MergeTvShows;

/// <summary>
/// One-shot prod patch for #1587 Toxic Empire episodes: identity, air date, duration,
/// per-episode SSR synopses (not the shared series OG blurb), episode landscape stills at
/// w=2000 on discovery+, and HBO Max sibling URLs (URL-only; same WBD ids as discovery+).
/// </summary>
public class EnrichToxicEmpireMeta(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IPlayableSearchDocumentSwap searchSwap,
    ILogger<EnrichToxicEmpireMeta> logger)
{
    private static readonly Guid ShowId = Guid.Parse("11aeb4e2-5991-4bf2-869a-26fd91082dce");
    private static readonly Guid Episode1Id = Guid.Parse("223bd5be-f89d-4f43-8b06-7db58b902757");
    private static readonly Guid Episode2Id = Guid.Parse("b7bf80f4-13ce-409d-9080-b2ae6d52ee4d");

    private const int ShowTmdbId = 343245;
    private const string ShowImdbId = "tt50289581";
    private const long ShowTvdbId = 485552;

    private const int Episode1TmdbId = 8166464;
    private const long Episode1TvdbId = 12103497;
    private const int Episode2TmdbId = 8179652;
    private const long Episode2TvdbId = 12103498;

    private static readonly DateOnly AirDate = new(2026, 11, 25);

    private static readonly Uri Episode1MaxUrl = new(
        "https://www.max.com/show/205d71fa-f5c5-46f7-80bd-b6350c107fa7/s1/e1-rise-of-top-g/e1d03033-ad8c-4b9a-9305-2042fedc750c");
    private static readonly Uri Episode2MaxUrl = new(
        "https://www.max.com/show/205d71fa-f5c5-46f7-80bd-b6350c107fa7/s1/e2-pimping-hoes-degree/d82e68d7-e47c-4aeb-914d-65584412e707");

    // Landscape episode stills at w=2000 — not series cover / not portrait; discoveryPlus only (no Max/identity dup).
    private static readonly Uri Episode1Image = new(
        "https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/e1d03033-ad8c-4b9a-9305-2042fedc750c/896e6c56-7d94-4f0b-ab91-c2fda2bdcbf1.jpeg?host=wbd-images.prod-vod.h264.io&partner=beamcom&w=2000");
    private static readonly Uri Episode2Image = new(
        "https://beam-images.warnermediacdn.com/BEAM_LWM_DELIVERABLES/d82e68d7-e47c-4aeb-914d-65584412e707/18eebf3e-e467-11f1-a763-02ffe6ac1e43.jpeg?host=wbd-images.prod-vod.h264.io&partner=beamcom&w=2000");

    /// <summary>
    /// Discovery+ SSR Season/Episode rich-text (not the shared series og:description blurb).
    /// </summary>
    private const string Episode1Synopsis =
        "Andrew Tate promises wealth, confidence, and brotherhood. Former followers offer a rare look inside the exclusive War Room as Andrew and Tristan build their empire and alleged victims share accounts that challenge their public image.";

    private const string Episode2Synopsis =
        "The Tate brothers' upbringing offers insight into their ambitions. A former member explores the PHD program, an alleged victim shares her account and Coconut Kitty's family offers a closer look at her life and connection to Tristan.";

    private static readonly TimeSpan Episode1Length = TimeSpan.FromMinutes(42);
    private static readonly TimeSpan Episode2Length = TimeSpan.FromMinutes(44);

    public async Task<int> Process(bool apply, CancellationToken cancellationToken)
    {
        var show = await tvShowRepository.GetTvShow(ShowId);
        if (show is null)
        {
            logger.LogError("Show '{id}' not found.", ShowId);
            return 1;
        }

        var e1 = await tvShowEpisodeRepository.GetBy(item => item.Id == Episode1Id);
        var e2 = await tvShowEpisodeRepository.GetBy(item => item.Id == Episode2Id);
        if (e1 is null || e2 is null)
        {
            logger.LogError("Expected episodes missing (e1={e1}, e2={e2}).", e1?.Id, e2?.Id);
            return 1;
        }

        logger.LogWarning(
            "Plan: show identity; episodes air={air}, lengths 42m/44m, per-episode SSR synopses, Max URLs (no image), landscape still w=2000 on discoveryPlus only, clear identity image. Apply={apply}.",
            AirDate, apply);

        if (!apply)
        {
            logger.LogWarning("Dry-run only. Re-run with --apply --enrich-toxic-empire-meta.");
            return 0;
        }

        show.TmdbId = ShowTmdbId;
        show.ImdbId = ShowImdbId;
        show.TvdbId = ShowTvdbId;
        await tvShowRepository.Save(show);

        ApplyEpisode(e1, show, Episode1TmdbId, Episode1TvdbId, Episode1Length, Episode1MaxUrl, Episode1Image, Episode1Synopsis);
        ApplyEpisode(e2, show, Episode2TmdbId, Episode2TvdbId, Episode2Length, Episode2MaxUrl, Episode2Image, Episode2Synopsis);
        await tvShowEpisodeRepository.Save(e1);
        await tvShowEpisodeRepository.Save(e2);

        var docs = new List<EpisodeSearchRecord>
        {
            CataloguePlayableSearchDocument.For(e1),
            CataloguePlayableSearchDocument.For(e2)
        };
        if (!await searchSwap.UploadAsync(docs, cancellationToken))
        {
            logger.LogError("Cosmos patched but search upload failed.");
            return 3;
        }

        logger.LogWarning(
            "Toxic Empire meta enrichment applied (identity, release, duration, per-episode SSR synopses, Max URL-only, landscape discoveryPlus still w=2000).");
        return 0;
    }

    private static void ApplyEpisode(
        TvShowEpisode episode,
        TvShow show,
        int tmdbId,
        long tvdbId,
        TimeSpan length,
        Uri maxUrl,
        Uri episodeImage,
        string synopsis)
    {
        episode.TmdbId = tmdbId;
        episode.TvdbId = tvdbId;
        episode.SeriesTmdbId = show.TmdbId;
        episode.SeriesImdbId = show.ImdbId;
        episode.SeriesTvdbId = show.TvdbId;
        episode.Length = length;
        episode.Description = synopsis;
        // Identity image cleared: still lives on discoveryPlus only (avoids duplicate Images-tab entries).
        episode.Image = null;
        episode.SetTvShowProperties(show);
        episode.SetRelease(CatalogueRelease.FromDate(AirDate));

        var services = episode.Services ?? new Dictionary<string, ServiceLink>();
        var discoveryKey = StreamingServiceWire.ToKey(StreamingService.DiscoveryPlus);
        if (services.TryGetValue(discoveryKey, out var discovery) && discovery is not null)
        {
            discovery.Image = episodeImage;
        }

        var maxKey = StreamingServiceWire.ToKey(StreamingService.HboMax);
        services[maxKey] = new ServiceLink { Url = maxUrl };
        episode.Services = services;
    }
}
