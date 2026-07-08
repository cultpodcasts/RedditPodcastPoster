using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

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
        Episode? matchingEpisode)
    {
        var (addedSpotify, addedApple, addedYouTube, addedBBC, addedInternetArchive) =
            (false, false, false, false, false);

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
                    idSelector: e => e.AppleId?.ToString(),
                    urlSelector: e => e.Urls.Apple);
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
                    idSelector: e => e.SpotifyId,
                    urlSelector: e => e.Urls.Spotify);
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
                    idSelector: e => e.YouTubeId,
                    urlSelector: e => e.Urls.YouTube);
                addedYouTube |= outcome.PlatformLinkAdded;
                episodeResult = MergeEpisodeResult(episodeResult, outcome);
            }
        }

        if (categorisedItem.ResolvedNonPodcastServiceItem != null && matchingEpisode != null)
        {
            if (matchingEpisode.Urls.BBC == null && categorisedItem.ResolvedNonPodcastServiceItem.BBCUrl != null)
            {
                addedBBC = true;
                matchingEpisode.Urls.BBC = categorisedItem.ResolvedNonPodcastServiceItem.BBCUrl;
                episodeResult = SubmitResultState.Enriched;
                logger.LogInformation(
                    "Enriched episode '{matchingEpisodeId}' with bbc details with bbc-url {resolvedNonPodcastServiceItemBBCUrl}.",
                    matchingEpisode.Id, categorisedItem.ResolvedNonPodcastServiceItem.BBCUrl);
            }

            if (matchingEpisode.Urls.InternetArchive == null &&
                categorisedItem.ResolvedNonPodcastServiceItem.InternetArchiveUrl != null)
            {
                addedInternetArchive = true;
                matchingEpisode.Urls.InternetArchive =
                    categorisedItem.ResolvedNonPodcastServiceItem.InternetArchiveUrl;
                episodeResult = SubmitResultState.Enriched;
                logger.LogInformation(
                    "Enriched episode '{matchingEpisodeId}' with internet-archive details with internet-archive-url {resolvedNonPodcastServiceItemInternetArchiveUrl}.",
                    matchingEpisode.Id, categorisedItem.ResolvedNonPodcastServiceItem.InternetArchiveUrl);
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

            if (matchingEpisode.Images?.Other == null && categorisedItem.ResolvedNonPodcastServiceItem.Image != null)
            {
                matchingEpisode.Images ??= new EpisodeImages();
                matchingEpisode.Images.YouTube = categorisedItem.ResolvedNonPodcastServiceItem.Image;
            }
        }

        return new ApplyResolvePodcastServicePropertiesResponse(podcastResult, episodeResult,
            new SubmitEpisodeDetails(addedSpotify, addedApple, addedYouTube, [], addedBBC, addedInternetArchive));
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
}
