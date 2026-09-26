using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.UrlSubmission.Services;

public class UrlMembershipLookup(
    IEpisodeRepository episodeRepository,
    IPodcastRepository podcastRepository,
    INonPodcastServiceAdapterResolver nonPodcastServiceAdapterResolver,
    IOptions<SubmitContentTypesOptions> submitContentTypes,
    IFilmRepository films,
    ITvShowEpisodeRepository tvShowEpisodes,
    INewsReportRepository newsReports)
    : IUrlMembershipLookup
{
    private bool ContentTypesEnabled => submitContentTypes.Value is { Enabled: true };

    public async Task<UrlMembershipLookupResult> Lookup(Uri url, CancellationToken cancellationToken)
    {
        if (ContentTypesEnabled && SubmitContentClassifier.IsBbcNews(url))
        {
            return new UrlMembershipLookupResult(
                false,
                UrlMembershipLookupKinds.Unrecognised,
                ContentKind: SubmitClassification.NewsReport);
        }

        var kind = Classify(url, out var storedUrlEquals, out var streamingService);
        if (kind == UrlMembershipLookupKinds.Unrecognised || storedUrlEquals is null)
        {
            return new UrlMembershipLookupResult(false, UrlMembershipLookupKinds.Unrecognised);
        }

        var storedMatches = episodeRepository.GetAllBy(storedUrlEquals);
        var matchingPodcastIds = storedMatches is null
            ? []
            : (await storedMatches.Select(x => x.PodcastId).ToListAsync(cancellationToken))
            .Distinct()
            .ToList();

        if (matchingPodcastIds.Count > 1)
        {
            return WithContentKind(new UrlMembershipLookupResult(
                false,
                Kind: kind,
                Ambiguous: true,
                PodcastIds: matchingPodcastIds,
                Service: streamingService));
        }

        if (matchingPodcastIds.Count == 1)
        {
            var podcastId = matchingPodcastIds[0];
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast != null)
            {
                return WithContentKind(new UrlMembershipLookupResult(
                    true,
                    Kind: kind,
                    PodcastId: podcast.Id,
                    PodcastName: podcast.Name,
                    Service: streamingService,
                    ContentKind: SubmitClassification.Episode,
                    ParentName: podcast.Name));
            }
        }

        var other = await FindOtherPlayable(url, kind, streamingService);
        if (other != null)
        {
            return other;
        }

        // Unknown streaming: classify only — prepare owns HTML fetch / show-name extract.
        return new UrlMembershipLookupResult(
            false,
            kind,
            PodcastName: null,
            Service: streamingService);
    }

    private UrlMembershipLookupResult WithContentKind(UrlMembershipLookupResult result)
    {
        if (!ContentTypesEnabled || result.ContentKind != null)
        {
            return ContentTypesEnabled
                ? result
                : result with { ContentKind = null, ParentName = null };
        }

        return result with
        {
            ContentKind = SubmitClassification.Episode,
            ParentName = result.PodcastName
        };
    }

    private async Task<UrlMembershipLookupResult?> FindOtherPlayable(
        Uri url,
        string kind,
        string? serviceKey)
    {
        if (!ContentTypesEnabled || string.IsNullOrWhiteSpace(serviceKey))
        {
            return null;
        }

        var canonical = CanonicalStoredUrl(url);
        var film = await films.GetBy(item =>
            item.Services != null && item.Services[serviceKey].Url == canonical);
        if (film != null)
        {
            return new UrlMembershipLookupResult(
                true,
                Kind: kind,
                Service: serviceKey,
                ContentKind: SubmitClassification.Film);
        }

        var episode = await tvShowEpisodes.GetBy(item =>
            item.Services != null && item.Services[serviceKey].Url == canonical);
        if (episode != null)
        {
            return new UrlMembershipLookupResult(
                true,
                Kind: kind,
                Service: serviceKey,
                ContentKind: SubmitClassification.TvShowEpisode,
                ParentName: episode.TvShowName);
        }

        var report = await newsReports.GetBy(item =>
            item.Services != null && item.Services[serviceKey].Url == canonical);
        if (report != null)
        {
            return new UrlMembershipLookupResult(
                true,
                Kind: kind,
                Service: serviceKey,
                ContentKind: SubmitClassification.NewsReport,
                ParentName: report.NewsOrganisationName);
        }

        return null;
    }

    private Uri CanonicalStoredUrl(Uri url) =>
        nonPodcastServiceAdapterResolver.ForSubmit(url)?.CanonicalStoredUrl(url) ?? url;

    private string Classify(
        Uri url,
        out Expression<Func<Episode, bool>>? storedUrlEquals,
        out string? streamingService)
    {
        streamingService = null;
        var key = StreamingServiceCatalog.TryResolveKey(url);
        if (key == ServiceKeys.Spotify)
        {
            storedUrlEquals = SpotifyStoredEquals(url);
            return UrlMembershipLookupKinds.PodcastService;
        }

        if (key == ServiceKeys.Apple)
        {
            storedUrlEquals = AppleStoredEquals(url);
            return UrlMembershipLookupKinds.PodcastService;
        }

        if (key == ServiceKeys.YouTube)
        {
            storedUrlEquals = YouTubeStoredEquals(url);
            return UrlMembershipLookupKinds.PodcastService;
        }

        var adapter = nonPodcastServiceAdapterResolver.ForSubmit(url);
        if (adapter != null)
        {
            storedUrlEquals = adapter.StoredUrlEquals(url);
            // Prefer catalogue host/path resolution (bbcSounds vs bbcIplayer). A matched streaming
            // adapter without a catalog key is an invariant violation — throw rather than guess.
            streamingService = key ?? throw new InvalidOperationException(
                $"Streaming adapter matched '{url}' but StreamingServiceCatalog.TryResolveKey returned null.");
            return UrlMembershipLookupKinds.Streaming;
        }

        storedUrlEquals = null;
        return UrlMembershipLookupKinds.Unrecognised;
    }

    private static Expression<Func<Episode, bool>> SpotifyStoredEquals(Uri url)
    {
        var episodeId = SpotifyIdResolver.GetEpisodeId(url);
        var cleaned = string.IsNullOrWhiteSpace(episodeId) ? url : url.CleanSpotifyUrl();
        if (string.IsNullOrWhiteSpace(episodeId))
        {
            return episode =>
                episode.Services != null && episode.Services[ServiceKeys.Spotify].Url == url;
        }

        return episode =>
            (episode.Ids != null && episode.Ids.Spotify == episodeId) ||
            (episode.Services != null &&
             (episode.Services[ServiceKeys.Spotify].Url == url ||
              episode.Services[ServiceKeys.Spotify].Url == cleaned));
    }

    private static Expression<Func<Episode, bool>> AppleStoredEquals(Uri url)
    {
        var episodeId = AppleIdResolver.GetEpisodeId(url);
        var cleaned = url.CleanAppleUrl();
        if (episodeId is null)
        {
            return episode =>
                episode.Services != null &&
                (episode.Services[ServiceKeys.Apple].Url == url ||
                 episode.Services[ServiceKeys.Apple].Url == cleaned);
        }

        return episode =>
            (episode.Ids != null && episode.Ids.Apple == episodeId) ||
            (episode.Services != null &&
             (episode.Services[ServiceKeys.Apple].Url == url ||
              episode.Services[ServiceKeys.Apple].Url == cleaned));
    }

    private static Expression<Func<Episode, bool>> YouTubeStoredEquals(Uri url)
    {
        var episodeId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(episodeId))
        {
            return episode =>
                episode.Services != null && episode.Services[ServiceKeys.YouTube].Url == url;
        }

        return episode =>
            (episode.Ids != null && episode.Ids.YouTube == episodeId) ||
            (episode.Services != null && episode.Services[ServiceKeys.YouTube].Url == url);
    }
}
