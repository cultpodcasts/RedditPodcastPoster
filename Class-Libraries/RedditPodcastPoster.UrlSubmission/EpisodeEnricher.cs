using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public class EpisodeEnricher(
    IDescriptionHelper descriptionHelper,
    ILogger<EpisodeEnricher> logger) : IEpisodeEnricher
{
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
                if (!matchingEpisode.AppleId.HasValue && categorisedItem.ResolvedAppleItem.EpisodeId.HasValue)
                {
                    addedApple = true;
                    matchingEpisode.AppleId = categorisedItem.ResolvedAppleItem.EpisodeId;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with apple details with apple-id {resolvedAppleItemEpisodeId}.",
                        matchingEpisode.Id, categorisedItem.ResolvedAppleItem.EpisodeId);
                }

                if (matchingEpisode.Urls.Apple == null && categorisedItem.ResolvedAppleItem.Url != null)
                {
                    addedApple = true;
                    matchingEpisode.Urls.Apple = categorisedItem.ResolvedAppleItem.Url;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with apple details with apple-url {resolvedAppleItemUrl}.",
                        matchingEpisode.Id, categorisedItem.ResolvedAppleItem.Url);
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

                if (matchingEpisode.Images?.Apple == null && categorisedItem.ResolvedAppleItem.Image != null)
                {
                    matchingEpisode.Images ??= new EpisodeImages();
                    matchingEpisode.Images.Apple = categorisedItem.ResolvedAppleItem.Image;
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
                if (string.IsNullOrWhiteSpace(matchingEpisode.SpotifyId) &&
                    !string.IsNullOrWhiteSpace(categorisedItem.ResolvedSpotifyItem.EpisodeId))
                {
                    addedSpotify = true;
                    matchingEpisode.SpotifyId = categorisedItem.ResolvedSpotifyItem.EpisodeId;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with spotify details with spotify-id {resolvedSpotifyItemEpisodeId}.",
                        matchingEpisode.Id, categorisedItem.ResolvedSpotifyItem.EpisodeId);
                }

                if (matchingEpisode.Urls.Spotify == null && categorisedItem.ResolvedSpotifyItem.Url != null)
                {
                    addedSpotify = true;
                    matchingEpisode.Urls.Spotify = categorisedItem.ResolvedSpotifyItem.Url;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with spotify details with spotify-url {resolvedSpotifyItemUrl}.",
                        matchingEpisode.Id, categorisedItem.ResolvedSpotifyItem.Url);
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

                if (matchingEpisode.Images?.Spotify == null && categorisedItem.ResolvedSpotifyItem.Image != null)
                {
                    matchingEpisode.Images ??= new EpisodeImages();
                    matchingEpisode.Images.Spotify = categorisedItem.ResolvedSpotifyItem.Image;
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
                if (string.IsNullOrWhiteSpace(matchingEpisode.YouTubeId) &&
                    !string.IsNullOrWhiteSpace(categorisedItem.ResolvedYouTubeItem.EpisodeId))
                {
                    addedYouTube = true;
                    matchingEpisode.YouTubeId = categorisedItem.ResolvedYouTubeItem.EpisodeId;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with youtube details with youtube-id {resolvedYouTubeItemEpisodeId}.",
                        matchingEpisode.Id, categorisedItem.ResolvedYouTubeItem.EpisodeId);
                }

                if (matchingEpisode.Urls.YouTube == null && categorisedItem.ResolvedYouTubeItem.Url != null)
                {
                    addedYouTube = true;
                    matchingEpisode.Urls.YouTube = categorisedItem.ResolvedYouTubeItem.Url;
                    episodeResult = SubmitResultState.Enriched;
                    logger.LogInformation(
                        "Enriched episode '{matchingEpisodeId}' with youtube details with youtube-url {resolvedYouTubeItem.}.",
                        matchingEpisode.Id, categorisedItem.ResolvedYouTubeItem.Url);
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

                if (matchingEpisode.Images?.YouTube == null && categorisedItem.ResolvedYouTubeItem.Image != null)
                {
                    matchingEpisode.Images ??= new EpisodeImages();
                    matchingEpisode.Images.YouTube = categorisedItem.ResolvedYouTubeItem.Image;
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
}