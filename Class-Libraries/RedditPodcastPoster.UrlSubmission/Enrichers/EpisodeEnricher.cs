using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Enrichers;

public class EpisodeEnricher(
    IDescriptionHelper descriptionHelper,
    IPlatformEnrichmentApplicator enrichmentApplicator,
    ILogger<EpisodeEnricher> logger) : IEpisodeEnricher
{
    private readonly ResolvedAppleItemAdapter _appleItemAdapter = new();
    private readonly ResolvedSpotifyItemAdapter _spotifyItemAdapter = new();
    private readonly ResolvedYouTubeItemAdapter _youTubeItemAdapter = new();

    public ApplyResolvePodcastServicePropertiesResponse ApplyResolvedPodcastServiceProperties(
        Podcast matchingPodcast,
        CategorisedItem categorisedItem,
        Episode? matchingEpisode,
        bool refreshMeta = false)
    {
        var (addedSpotify, addedApple, addedYouTube, addedBBC, addedInternetArchive) =
            (false, false, false, false, false);
        var addedExtraKeys = new HashSet<string>(StringComparer.Ordinal);

        var podcastResult = SubmitResultState.None;
        var episodeResult = SubmitResultState.None;
        if (matchingEpisode != null)
        {
            episodeResult = SubmitResultState.EpisodeAlreadyExists;
            logger.LogInformation(
                "Applying to episode with title '{matchingEpisodeTitle}' and id '{matchingEpisodeId}'.",
                matchingEpisode.Title, matchingEpisode.Id);
        }

        if (categorisedItem.ResolvedAppleItem != null)
        {
            if (!matchingPodcast.AppleId.HasValue)
            {
                matchingPodcast.AppleId = categorisedItem.ResolvedAppleItem.ShowId;
                podcastResult = SubmitResultState.Enriched;
                logger.LogInformation(
                    "Enriched podcast '{matchingPodcastId}' with apple details with apple-id {resolvedAppleItemShowId}.",
                    matchingPodcast.Id, categorisedItem.ResolvedAppleItem.ShowId);
            }

            if (matchingEpisode != null)
            {
                var outcome = ApplyResolvedPlatformEnrichment(
                    matchingPodcast,
                    matchingEpisode,
                    BuildCandidate(
                        _appleItemAdapter.Adapt(categorisedItem.ResolvedAppleItem.ToAdapterInput()),
                        categorisedItem.ResolvedAppleItem.EpisodeDescription,
                        categorisedItem),
                    platformName: "apple",
                    logIdProperty: "apple-id",
                    idSelector: e => EpisodeServicePresence.AppleEpisodeId(e)?.ToString(),
                    urlSelector: e => EpisodeServicePresence.TryGetUrl(e, ServiceKeys.Apple));
                addedApple |= outcome.PlatformLinkAdded;
                episodeResult = MergeEpisodeResult(episodeResult, outcome);
            }
        }

        if (categorisedItem.ResolvedSpotifyItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.SpotifyId))
            {
                matchingPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem.ShowId;
                podcastResult = SubmitResultState.Enriched;
                logger.LogInformation(
                    "Enriched podcast '{matchingPodcastId}' with spotify details with spotify-id {resolvedSpotifyItemShowId}.",
                    matchingPodcast.Id, categorisedItem.ResolvedSpotifyItem.ShowId);
            }

            if (matchingEpisode != null)
            {
                var outcome = ApplyResolvedPlatformEnrichment(
                    matchingPodcast,
                    matchingEpisode,
                    BuildCandidate(
                        _spotifyItemAdapter.Adapt(categorisedItem.ResolvedSpotifyItem.ToAdapterInput()),
                        categorisedItem.ResolvedSpotifyItem.EpisodeDescription,
                        categorisedItem),
                    platformName: "spotify",
                    logIdProperty: "spotify-id",
                    idSelector: e => EpisodeServicePresence.SpotifyEpisodeId(e),
                    urlSelector: e => EpisodeServicePresence.TryGetUrl(e, ServiceKeys.Spotify));
                addedSpotify |= outcome.PlatformLinkAdded;
                episodeResult = MergeEpisodeResult(episodeResult, outcome);
            }
        }

        if (categorisedItem.ResolvedYouTubeItem != null)
        {
            if (string.IsNullOrWhiteSpace(matchingPodcast.YouTubeChannelId))
            {
                matchingPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem.ShowId;
                matchingPodcast.YouTubePublicationOffset = Constants.DefaultMatchingPodcastYouTubePublishingDelay.Ticks;
                podcastResult = SubmitResultState.Enriched;
                logger.LogInformation(
                    "Enriched podcast '{matchingPodcastId}' with youtube details with youtube-id {resolvedYouTubeItemShowId}.",
                    matchingPodcast.Id, categorisedItem.ResolvedYouTubeItem.ShowId);
            }

            if (matchingEpisode != null)
            {
                var outcome = ApplyResolvedPlatformEnrichment(
                    matchingPodcast,
                    matchingEpisode,
                    BuildCandidate(
                        _youTubeItemAdapter.Adapt(categorisedItem.ResolvedYouTubeItem.ToAdapterInput()),
                        categorisedItem.ResolvedYouTubeItem.EpisodeDescription,
                        categorisedItem),
                    platformName: "youtube",
                    logIdProperty: "youtube-id",
                    idSelector: e => EpisodeServicePresence.YouTubeEpisodeId(e),
                    urlSelector: e => EpisodeServicePresence.TryGetUrl(e, ServiceKeys.YouTube));
                addedYouTube |= outcome.PlatformLinkAdded;
                episodeResult = MergeEpisodeResult(episodeResult, outcome);
            }
        }

        if (categorisedItem.ResolvedNonPodcastServiceItem != null && matchingEpisode != null)
        {
            if (refreshMeta)
            {
                episodeResult = ApplyNonPodcastRefreshMeta(
                    matchingEpisode,
                    categorisedItem,
                    addedExtraKeys,
                    ref addedBBC,
                    ref addedInternetArchive,
                    episodeResult);
            }
            else
            {
                if (!EpisodeServicePresence.HasUrl(matchingEpisode, ServiceKeys.BbcIplayer) &&
                    !EpisodeServicePresence.HasUrl(matchingEpisode, ServiceKeys.BbcSounds) &&
                    categorisedItem.ResolvedNonPodcastServiceItem.BBCUrl != null)
                {
                    addedBBC = true;
                    var bbcUrl = categorisedItem.ResolvedNonPodcastServiceItem.BBCUrl;
                    var bbcKey = ServiceCatalog.TryResolveKey(bbcUrl) ?? ServiceKeys.BbcSounds;
                    EpisodeServicePresence.Upsert(
                        matchingEpisode,
                        bbcKey,
                        bbcUrl,
                        categorisedItem.ResolvedNonPodcastServiceItem.Image);
                    addedExtraKeys.Add(bbcKey);
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with bbc details with bbc-url {resolvedNonPodcastServiceItemBBCUrl}.",
                        matchingEpisode.Id, categorisedItem.ResolvedNonPodcastServiceItem.BBCUrl);
                }

                if (!EpisodeServicePresence.HasUrl(matchingEpisode, ServiceKeys.InternetArchive) &&
                    categorisedItem.ResolvedNonPodcastServiceItem.InternetArchiveUrl != null)
                {
                    addedInternetArchive = true;
                    EpisodeServicePresence.Upsert(
                        matchingEpisode,
                        ServiceKeys.InternetArchive,
                        categorisedItem.ResolvedNonPodcastServiceItem.InternetArchiveUrl,
                        null);
                    addedExtraKeys.Add(ServiceKeys.InternetArchive);
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with internet-archive details with internet-archive-url {resolvedNonPodcastServiceItemInternetArchiveUrl}.",
                        matchingEpisode.Id, categorisedItem.ResolvedNonPodcastServiceItem.InternetArchiveUrl);
                }

                if (categorisedItem.ResolvedNonPodcastServiceItem.BBCUrl == null &&
                    categorisedItem.ResolvedNonPodcastServiceItem.InternetArchiveUrl == null &&
                    categorisedItem.ResolvedNonPodcastServiceItem.Url is { } streamingUrl)
                {
                    var streamingKey = ServiceCatalog.TryResolveKey(streamingUrl);
                    if (streamingKey != null &&
                        !EpisodeServicePresence.HasUrl(matchingEpisode, streamingKey))
                    {
                        EpisodeServicePresence.Upsert(
                            matchingEpisode,
                            streamingKey,
                            streamingUrl,
                            categorisedItem.ResolvedNonPodcastServiceItem.Image);
                        addedExtraKeys.Add(streamingKey);
                        episodeResult = SubmitResultState.Enriched;
                        logger.LogInformation(
                            "Enriched episode '{matchingEpisodeId}' with {serviceKey} url {streamingUrl}.",
                            matchingEpisode.Id, streamingKey, streamingUrl);
                    }
                }

                if (matchingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
                    categorisedItem.ResolvedNonPodcastServiceItem.Release.HasValue &&
                    categorisedItem.ResolvedNonPodcastServiceItem.Release.Value.TimeOfDay != TimeSpan.Zero)
                {
                    matchingEpisode.Release = categorisedItem.ResolvedNonPodcastServiceItem.Release.Value;
                    episodeResult = SubmitResultState.Enriched;
                }

                var description =
                    descriptionHelper.CollapseDescription(categorisedItem.ResolvedNonPodcastServiceItem.Description) ??
                    descriptionHelper.EnrichMissingDescription(categorisedItem);
                if (matchingEpisode.Description.Trim().EndsWith("...") &&
                    description.Length > matchingEpisode.Description.Length)
                {
                    matchingEpisode.Description = description;
                    episodeResult = SubmitResultState.Enriched;
                }

                if (categorisedItem.ResolvedNonPodcastServiceItem.Image is { } nonPodcastImage)
                {
                    var imageKey = ResolveNonPodcastImageKey(categorisedItem.ResolvedNonPodcastServiceItem);
                    if (imageKey != null &&
                        EpisodeServicePresence.TryFillMissing(
                            matchingEpisode, imageKey, null, nonPodcastImage))
                    {
                        episodeResult = SubmitResultState.Enriched;
                    }
                }
            }
        }

        return new ApplyResolvePodcastServicePropertiesResponse(podcastResult, episodeResult,
            new SubmitEpisodeDetails(
                addedSpotify,
                addedApple,
                addedYouTube,
                [],
                addedBBC,
                addedInternetArchive,
                Vimeo: addedExtraKeys.Contains(ServiceKeys.Vimeo),
                Netflix: addedExtraKeys.Contains(ServiceKeys.Netflix),
                AmazonPrime: addedExtraKeys.Contains(ServiceKeys.AmazonPrime),
                ExtraServiceKeys: addedExtraKeys.Count == 0 ? null : addedExtraKeys.ToArray()));
    }

    private static SubmitResultState MergeEpisodeResult(
        SubmitResultState current,
        ResolvedPlatformApplyOutcome outcome) =>
        outcome.EpisodeEnriched ? SubmitResultState.Enriched : current;

    private ResolvedPlatformApplyOutcome ApplyResolvedPlatformEnrichment(
        Podcast podcast,
        Episode episode,
        EpisodeCandidate candidate,
        string platformName,
        string logIdProperty,
        Func<Episode, string?> idSelector,
        Func<Episode, Uri?> urlSelector)
    {
        var missingId = string.IsNullOrWhiteSpace(idSelector(episode));
        var missingUrl = urlSelector(episode) == null;

        var result = enrichmentApplicator.Apply(podcast, episode, candidate);
        var platformLinkAdded = false;

        if (missingId && !string.IsNullOrWhiteSpace(idSelector(episode)))
        {
            platformLinkAdded = true;
            logger.LogInformation(
                "Enriched episode '{matchingEpisodeId}' with {platformName} details with {logIdProperty} {platformId}.",
                episode.Id,
                platformName,
                logIdProperty,
                idSelector(episode));
        }

        if (missingUrl && urlSelector(episode) != null)
        {
            platformLinkAdded = true;
            logger.LogInformation(
                "Enriched episode '{matchingEpisodeId}' with {platformName} details with {platformName}-url {platformUrl}.",
                episode.Id,
                platformName,
                platformName,
                urlSelector(episode));
        }

        return new ResolvedPlatformApplyOutcome(platformLinkAdded, result.Updated);
    }

    private EpisodeCandidate BuildCandidate(
        EpisodeCandidate candidate,
        string? resolvedDescription,
        CategorisedItem categorisedItem)
    {
        var description =
            descriptionHelper.CollapseDescription(resolvedDescription) ??
            descriptionHelper.EnrichMissingDescription(categorisedItem);
        return candidate with { Description = description };
    }

    private SubmitResultState ApplyNonPodcastRefreshMeta(
        Episode matchingEpisode,
        CategorisedItem categorisedItem,
        HashSet<string> addedExtraKeys,
        ref bool addedBBC,
        ref bool addedInternetArchive,
        SubmitResultState episodeResult)
    {
        var item = categorisedItem.ResolvedNonPodcastServiceItem!;
        var changed = false;

        if (!string.IsNullOrWhiteSpace(item.Title) &&
            !string.Equals(matchingEpisode.Title, item.Title, StringComparison.Ordinal))
        {
            matchingEpisode.Title = item.Title;
            changed = true;
        }

        var description =
            descriptionHelper.CollapseDescription(item.Description) ??
            descriptionHelper.EnrichMissingDescription(categorisedItem);
        if (!string.IsNullOrWhiteSpace(description) &&
            !string.Equals(matchingEpisode.Description, description, StringComparison.Ordinal))
        {
            matchingEpisode.Description = description;
            changed = true;
        }

        if (item.Release is { } release && matchingEpisode.Release != release)
        {
            matchingEpisode.Release = release;
            changed = true;
        }

        if (item.Duration is { } duration &&
            duration > TimeSpan.Zero &&
            matchingEpisode.Length != duration)
        {
            matchingEpisode.Length = duration;
            changed = true;
        }

        if (item.BBCUrl is { } bbcUrl)
        {
            addedBBC = true;
            var bbcKey = ServiceCatalog.TryResolveKey(bbcUrl) ?? ServiceKeys.BbcSounds;
            EpisodeServicePresence.Upsert(matchingEpisode, bbcKey, bbcUrl, item.Image);
            addedExtraKeys.Add(bbcKey);
            changed = true;
        }
        else if (item.InternetArchiveUrl is { } internetArchiveUrl)
        {
            addedInternetArchive = true;
            EpisodeServicePresence.Upsert(
                matchingEpisode,
                ServiceKeys.InternetArchive,
                internetArchiveUrl,
                item.Image);
            addedExtraKeys.Add(ServiceKeys.InternetArchive);
            changed = true;
        }
        else if (item.Url is { } streamingUrl)
        {
            var streamingKey = ServiceCatalog.TryResolveKey(streamingUrl)
                               ?? ServiceCatalog.KeyFromUnknownHost(streamingUrl);
            if (streamingKey != null)
            {
                EpisodeServicePresence.Upsert(matchingEpisode, streamingKey, streamingUrl, item.Image);
                addedExtraKeys.Add(streamingKey);
                changed = true;
            }
        }
        else if (item.Image is { } imageOnly)
        {
            var imageKey = ResolveNonPodcastImageKey(item);
            if (imageKey != null)
            {
                var existingUrl = EpisodeServicePresence.TryGetUrl(matchingEpisode, imageKey);
                EpisodeServicePresence.Upsert(matchingEpisode, imageKey, existingUrl, imageOnly);
                changed = true;
            }
        }

        if (changed)
        {
            episodeResult = SubmitResultState.Enriched;
            logger.LogInformation(
                "Refresh-meta overwrote non-podcast fields on episode '{matchingEpisodeId}'.",
                matchingEpisode.Id);
        }

        return episodeResult;
    }

    private static string? ResolveNonPodcastImageKey(ResolvedNonPodcastServiceItem item)
    {
        if (item.BBCUrl is { } bbcForImage)
        {
            return ServiceCatalog.TryResolveKey(bbcForImage) ?? ServiceKeys.BbcSounds;
        }

        if (item.InternetArchiveUrl != null)
        {
            return ServiceKeys.InternetArchive;
        }

        if (item.Url is { } url)
        {
            return ServiceCatalog.TryResolveKey(url) ?? ServiceCatalog.KeyFromUnknownHost(url);
        }

        return null;
    }
}
