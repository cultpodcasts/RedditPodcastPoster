using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Playables;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
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
        if (ContentTypesEnabled && BbcNewsSubmitRejection.Applies(url))
        {
            // G5 (#1211): same rejection as submit, so the client can say so before any scrape.
            return new UrlMembershipLookupResult(
                false,
                UrlMembershipLookupKinds.Unrecognised,
                ContentKind: SubmitClassification.NewsReport,
                Rejected: true,
                RejectionMessage: BbcNewsSubmitRejection.Message);
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

        var other = await FindOtherPlayable(url, kind, streamingService, cancellationToken);
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

    public Task<CatalogueServiceUrlOwner?> FindCatalogueServiceUrlOwner(
        Uri url,
        CancellationToken cancellationToken)
    {
        var serviceKey = ServiceCatalog.TryResolvePodcastPlatformKey(url);
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return Task.FromResult<CatalogueServiceUrlOwner?>(null);
        }

        return QueryCatalogueOwner(CanonicalStoredUrl(url), serviceKey, cancellationToken);
    }

    private async Task<UrlMembershipLookupResult?> FindOtherPlayable(
        Uri url,
        string kind,
        string? serviceKey,
        CancellationToken cancellationToken)
    {
        if (!ContentTypesEnabled)
        {
            return null;
        }

        // Streaming URLs carry their catalog key. YouTube, Spotify and Apple URLs reach here only when no podcast
        // episode stores them, e.g. an episode attached to a film as a trailer (its row is deleted on attach).
        var ownerKey = serviceKey ??
                       (kind == UrlMembershipLookupKinds.PodcastService
                           ? ServiceCatalog.TryResolvePodcastPlatformKey(url)
                           : null);
        if (string.IsNullOrWhiteSpace(ownerKey))
        {
            return null;
        }

        var owner = await QueryCatalogueOwner(CanonicalStoredUrl(url), ownerKey, cancellationToken);
        if (owner is null)
        {
            return null;
        }

        return new UrlMembershipLookupResult(
            true,
            Kind: kind,
            Service: serviceKey,
            ContentKind: owner.ContentKind,
            ParentName: owner.ParentName);
    }

    private async Task<CatalogueServiceUrlOwner?> QueryCatalogueOwner(
        Uri canonical,
        string serviceKey,
        CancellationToken cancellationToken)
    {
        var stored = PodcastServiceUrlNormaliser.Stored(canonical, serviceKey);
        var spotifyId = serviceKey == ServiceKeys.Spotify ? SpotifyIdResolver.GetEpisodeId(canonical) : null;
        var appleId = serviceKey == ServiceKeys.Apple ? AppleIdResolver.GetEpisodeId(canonical) : null;
        var youTubeId = serviceKey == ServiceKeys.YouTube ? YouTubeIdResolver.Extract(canonical) : null;

        cancellationToken.ThrowIfCancellationRequested();
        var film = await films.GetBy(
            PlayableServiceUrlFilters.FilmHoldsServiceOrTrailerUrl(
                serviceKey, canonical, stored, spotifyId, appleId, youTubeId));
        if (film != null)
        {
            return new CatalogueServiceUrlOwner(SubmitClassification.Film, null, film.Id);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var episode = await tvShowEpisodes.GetBy(
            PlayableServiceUrlFilters.HoldsServiceUrlOrYouTubeAlternate<TvShowEpisode>(
                serviceKey, canonical, stored, youTubeId));
        if (episode != null)
        {
            return new CatalogueServiceUrlOwner(
                SubmitClassification.TvShowEpisode, episode.TvShowName, episode.Id, episode.TvShowId);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var report = await newsReports.GetBy(
            PlayableServiceUrlFilters.HoldsServiceUrlOrYouTubeAlternate<NewsReport>(
                serviceKey, canonical, stored, youTubeId));
        if (report != null)
        {
            return new CatalogueServiceUrlOwner(
                SubmitClassification.NewsReport, report.NewsOrganisationName, report.Id, report.NewsOrganisationId);
        }

        // Podcast-episode additionalYouTube only: after a YouTube-alternate attach (targetKind Episode)
        // the source row is deleted and the url lives only on another episode's additionalYouTube.
        // Primary podcast-episode matches stay on the existing per-podcast merge, so submit, discovery
        // and the indexer do not treat a live episode's own YouTube as catalogue-owned (#1068 / RPP #1411).
        if (youTubeId != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var podcastEpisode = await episodeRepository.GetBy(
                YouTubeAlternateEquals(canonical, stored, youTubeId));
            if (podcastEpisode != null)
            {
                return new CatalogueServiceUrlOwner(
                    SubmitClassification.Episode,
                    podcastEpisode.PodcastName,
                    podcastEpisode.Id,
                    podcastEpisode.PodcastId);
            }
        }

        return null;
    }

    public async Task<CatalogueServiceUrlOwner?> FindYouTubeHolderExcluding(
        string youTubeId,
        IReadOnlyCollection<Guid> exceptIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youTubeId);
        ArgumentNullException.ThrowIfNull(exceptIds);

        var url = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var canonical = CanonicalStoredUrl(url);
        var stored = PodcastServiceUrlNormaliser.Stored(canonical, ServiceKeys.YouTube);
        var except = exceptIds as IReadOnlySet<Guid> ?? exceptIds.ToHashSet();

        cancellationToken.ThrowIfCancellationRequested();
        var film = await films.GetBy(AndIdNotIn(
            PlayableServiceUrlFilters.FilmHoldsServiceOrTrailerUrl(
                ServiceKeys.YouTube, canonical, stored, youTubeId: youTubeId),
            except,
            item => item.Id));
        if (film != null)
        {
            return new CatalogueServiceUrlOwner(SubmitClassification.Film, null, film.Id);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var tvEpisode = await tvShowEpisodes.GetBy(AndIdNotIn(
            PlayableServiceUrlFilters.HoldsServiceUrlOrYouTubeAlternate<TvShowEpisode>(
                ServiceKeys.YouTube, canonical, stored, youTubeId),
            except,
            item => item.Id));
        if (tvEpisode != null)
        {
            return new CatalogueServiceUrlOwner(
                SubmitClassification.TvShowEpisode, tvEpisode.TvShowName, tvEpisode.Id, tvEpisode.TvShowId);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var report = await newsReports.GetBy(AndIdNotIn(
            PlayableServiceUrlFilters.HoldsServiceUrlOrYouTubeAlternate<NewsReport>(
                ServiceKeys.YouTube, canonical, stored, youTubeId),
            except,
            item => item.Id));
        if (report != null)
        {
            return new CatalogueServiceUrlOwner(
                SubmitClassification.NewsReport, report.NewsOrganisationName, report.Id, report.NewsOrganisationId);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var podcastEpisode = await episodeRepository.GetBy(AndIdNotIn(
            YouTubeStoredEquals(canonical),
            except,
            item => item.Id));
        if (podcastEpisode != null)
        {
            return new CatalogueServiceUrlOwner(
                SubmitClassification.Episode,
                podcastEpisode.PodcastName,
                podcastEpisode.Id,
                podcastEpisode.PodcastId);
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
        var cleaned = PodcastServiceUrlNormaliser.Stored(url, ServiceKeys.Spotify);
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
        var cleaned = PodcastServiceUrlNormaliser.Stored(url, ServiceKeys.Apple);
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

    /// <summary>
    /// A podcast episode that holds this YouTube id only as an <c>additionalYouTube</c> entry, not as its primary.
    /// </summary>
    private static Expression<Func<Episode, bool>> YouTubeAlternateEquals(Uri url, Uri storedUrl, string youTubeId) =>
        episode =>
            episode.AdditionalYouTube != null &&
            episode.AdditionalYouTube.Any(alternate =>
                alternate.YouTubeId == youTubeId || alternate.Url == url || alternate.Url == storedUrl);

    private static Expression<Func<T, bool>> AndIdNotIn<T>(
        Expression<Func<T, bool>> holds,
        IReadOnlySet<Guid> exceptIds,
        Expression<Func<T, Guid>> idSelector)
    {
        if (exceptIds.Count == 0)
        {
            return holds;
        }

        var parameter = idSelector.Parameters[0];
        Expression exclude = Expression.Constant(true);
        foreach (var id in exceptIds)
        {
            exclude = Expression.AndAlso(
                exclude,
                Expression.NotEqual(idSelector.Body, Expression.Constant(id)));
        }

        var notExcluded = Expression.Lambda<Func<T, bool>>(exclude, parameter);
        var combined = Expression.Parameter(typeof(T), "item");
        var body = Expression.AndAlso(
            ReplaceParameter(holds.Body, holds.Parameters[0], combined),
            ReplaceParameter(notExcluded.Body, notExcluded.Parameters[0], combined));
        return Expression.Lambda<Func<T, bool>>(body, combined);
    }

    private static Expression ReplaceParameter(Expression body, ParameterExpression from, ParameterExpression to) =>
        new ParameterReplacer(from, to).Visit(body)!;

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
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
            (episode.Services != null && episode.Services[ServiceKeys.YouTube].Url == url) ||
            (episode.AdditionalYouTube != null &&
             episode.AdditionalYouTube.Any(alternate =>
                 alternate.YouTubeId == episodeId || alternate.Url == url));
    }
}
