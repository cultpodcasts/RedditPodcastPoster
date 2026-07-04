using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public class EpisodeEnricher(
    IDescriptionHelper descriptionHelper,
    IEpisodePlatformApplier episodePlatformApplier,
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
                var missingAppleId = !matchingEpisode.AppleId.HasValue;
                var missingAppleUrl = matchingEpisode.Urls.Apple == null;

                ApplyPlatformLink(matchingEpisode, _appleItemAdapter.Adapt(ToInput(categorisedItem.ResolvedAppleItem)));

                if (missingAppleId && matchingEpisode.AppleId.HasValue)
                {
                    addedApple = true;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with apple details with apple-id {resolvedAppleItemEpisodeId}.",
                        matchingEpisode.Id, matchingEpisode.AppleId);
                }

                if (missingAppleUrl && matchingEpisode.Urls.Apple != null)
                {
                    addedApple = true;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with apple details with apple-url {resolvedAppleItemUrl}.",
                        matchingEpisode.Id, matchingEpisode.Urls.Apple);
                }

                if (matchingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
                    categorisedItem.ResolvedAppleItem.Release.TimeOfDay != TimeSpan.Zero)
                {
                    matchingEpisode.Release = categorisedItem.ResolvedAppleItem.Release;
                    episodeResult = SubmitResultState.Enriched;
                }

                var description =
                    descriptionHelper.CollapseDescription(categorisedItem.ResolvedAppleItem.EpisodeDescription) ??
                    descriptionHelper.EnrichMissingDescription(categorisedItem);
                if (string.IsNullOrWhiteSpace(matchingEpisode.Description) ||
                    (matchingEpisode.Description.EndsWith("...") &&
                     description.Length > matchingEpisode.Description.Length))
                {
                    matchingEpisode.Description = description;
                    episodeResult = SubmitResultState.Enriched;
                }
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
                var missingSpotifyId = string.IsNullOrWhiteSpace(matchingEpisode.SpotifyId);
                var missingSpotifyUrl = matchingEpisode.Urls.Spotify == null;

                ApplyPlatformLink(matchingEpisode, _spotifyItemAdapter.Adapt(ToInput(categorisedItem.ResolvedSpotifyItem)));

                if (missingSpotifyId && !string.IsNullOrWhiteSpace(matchingEpisode.SpotifyId))
                {
                    addedSpotify = true;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with spotify details with spotify-id {resolvedSpotifyItemEpisodeId}.",
                        matchingEpisode.Id, matchingEpisode.SpotifyId);
                }

                if (missingSpotifyUrl && matchingEpisode.Urls.Spotify != null)
                {
                    addedSpotify = true;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with spotify details with spotify-url {resolvedSpotifyItemUrl}.",
                        matchingEpisode.Id, matchingEpisode.Urls.Spotify);
                }

                var description =
                    descriptionHelper.CollapseDescription(categorisedItem.ResolvedSpotifyItem.EpisodeDescription) ??
                    descriptionHelper.EnrichMissingDescription(categorisedItem);
                if (matchingEpisode.Description.EndsWith("...") &&
                    description.Length > matchingEpisode.Description.Length)
                {
                    matchingEpisode.Description = description;
                    episodeResult = SubmitResultState.Enriched;
                }
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
                var missingYouTubeId = string.IsNullOrWhiteSpace(matchingEpisode.YouTubeId);
                var missingYouTubeUrl = matchingEpisode.Urls.YouTube == null;

                ApplyPlatformLink(matchingEpisode, _youTubeItemAdapter.Adapt(ToInput(categorisedItem.ResolvedYouTubeItem)));

                if (missingYouTubeId && !string.IsNullOrWhiteSpace(matchingEpisode.YouTubeId))
                {
                    addedYouTube = true;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with youtube details with youtube-id {resolvedYouTubeItemEpisodeId}.",
                        matchingEpisode.Id, matchingEpisode.YouTubeId);
                }

                if (missingYouTubeUrl && matchingEpisode.Urls.YouTube != null)
                {
                    addedYouTube = true;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with youtube details with youtube-url {resolvedYouTubeItem.}.",
                        matchingEpisode.Id, matchingEpisode.Urls.YouTube);
                }

                if (matchingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
                    categorisedItem.ResolvedYouTubeItem.Release.TimeOfDay != TimeSpan.Zero)
                {
                    matchingEpisode.Release = categorisedItem.ResolvedYouTubeItem.Release;
                    episodeResult = SubmitResultState.Enriched;
                }

                var description =
                    descriptionHelper.CollapseDescription(categorisedItem.ResolvedYouTubeItem.EpisodeDescription) ??
                    descriptionHelper.EnrichMissingDescription(categorisedItem);
                if (matchingEpisode.Description.Trim().EndsWith("...") &&
                    description.Length > matchingEpisode.Description.Length)
                {
                    matchingEpisode.Description = description;
                    episodeResult = SubmitResultState.Enriched;
                }
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

    private void ApplyPlatformLink(Episode matchingEpisode, EpisodeCandidate candidate)
    {
        episodePlatformApplier.ApplyFillMissing(
            matchingEpisode,
            new EpisodePlatformPatch(candidate.SourceLink, Description: null, Release: null));
    }

    private static ResolvedAppleItemInput ToInput(ResolvedAppleItem item) =>
        new(
            item.EpisodeId,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration,
            item.Url,
            item.Image);

    private static ResolvedSpotifyItemInput ToInput(ResolvedSpotifyItem item) =>
        new(
            item.EpisodeId,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration,
            item.Url,
            item.Image);

    private static ResolvedYouTubeItemInput ToInput(ResolvedYouTubeItem item) =>
        new(
            item.EpisodeId,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration,
            item.Url,
            item.Image);
}
