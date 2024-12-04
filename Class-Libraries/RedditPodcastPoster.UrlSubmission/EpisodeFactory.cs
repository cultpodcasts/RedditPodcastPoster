using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public class EpisodeFactory(
    IDescriptionHelper descriptionHelper,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<EpisodeFactory> logger
) : IEpisodeFactory
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public Episode CreateEpisode(CategorisedItem categorisedItem)
    {
        string title;
        DateTime release;
        TimeSpan length;
        bool @explicit;
        string description;

        switch (categorisedItem.Authority)
        {
            case Service.Apple:
                title = categorisedItem.ResolvedAppleItem!.EpisodeTitle;
                release = categorisedItem.ResolvedAppleItem.Release;
                length = categorisedItem.ResolvedAppleItem.Duration;
                @explicit = categorisedItem.ResolvedAppleItem.Explicit;
                description = categorisedItem.ResolvedAppleItem.EpisodeDescription;
                break;
            case Service.Spotify:
                title = categorisedItem.ResolvedSpotifyItem!.EpisodeTitle;
                release =
                    categorisedItem.ResolvedSpotifyItem.Release.TimeOfDay == TimeSpan.Zero &&
                    categorisedItem.ResolvedAppleItem != null
                        ? categorisedItem.ResolvedAppleItem.Release
                        : categorisedItem.ResolvedSpotifyItem.Release;
                length = categorisedItem.ResolvedSpotifyItem.Duration;
                @explicit = categorisedItem.ResolvedSpotifyItem.Explicit;
                description = categorisedItem.ResolvedSpotifyItem.EpisodeDescription;
                break;
            case Service.YouTube:
                title = categorisedItem.ResolvedYouTubeItem!.EpisodeTitle;
                release = categorisedItem.ResolvedYouTubeItem.Release;
                length = categorisedItem.ResolvedYouTubeItem.Duration;
                @explicit = categorisedItem.ResolvedYouTubeItem.Explicit;
                description = categorisedItem.ResolvedYouTubeItem.EpisodeDescription;
                break;
            case Service.Other:
                title = categorisedItem.ResolvedNonPodcastServiceItem!.Title!;
                release = categorisedItem.ResolvedNonPodcastServiceItem.Release ?? DateTime.MinValue;
                length = categorisedItem.ResolvedNonPodcastServiceItem.Duration ?? TimeSpan.Zero;
                @explicit = categorisedItem.ResolvedNonPodcastServiceItem.Explicit;
                description = categorisedItem.ResolvedNonPodcastServiceItem.Description ?? string.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(categorisedItem.Authority));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            description = descriptionHelper.EnrichMissingDescription(categorisedItem);
        }

        var newEpisode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = title,
            Release = release,
            Length = length,
            Explicit = @explicit,
            AppleId = categorisedItem.ResolvedAppleItem?.EpisodeId,
            SpotifyId = categorisedItem.ResolvedSpotifyItem?.EpisodeId ?? string.Empty,
            YouTubeId = categorisedItem.ResolvedYouTubeItem?.EpisodeId ?? string.Empty,
            Description = description,
            Urls = new ServiceUrls
            {
                Spotify = categorisedItem.ResolvedSpotifyItem?.Url,
                Apple = categorisedItem.ResolvedAppleItem?.Url,
                YouTube = categorisedItem.ResolvedYouTubeItem?.Url,
                BBC = categorisedItem.ResolvedNonPodcastServiceItem?.BBCUrl,
                InternetArchive = categorisedItem.ResolvedNonPodcastServiceItem?.InternetArchiveUrl
            }
        };
        if (categorisedItem.MatchingPodcast != null)
        {
            if (categorisedItem.MatchingPodcast.BypassShortEpisodeChecking.HasValue &&
                categorisedItem.MatchingPodcast.BypassShortEpisodeChecking.Value)
            {
                newEpisode.Ignored = false;
            }
            else
            {
                newEpisode.Ignored = length < _postingCriteria.MinimumDuration;
            }
        }
        else
        {
            newEpisode.Ignored = length < _postingCriteria.MinimumDuration;
        }

        if (categorisedItem.ResolvedAppleItem?.Image != null ||
            categorisedItem.ResolvedSpotifyItem?.Image != null ||
            categorisedItem.ResolvedYouTubeItem?.Image != null ||
            categorisedItem.ResolvedNonPodcastServiceItem?.Image != null)
        {
            newEpisode.Images = new EpisodeImages
            {
                Apple = categorisedItem.ResolvedAppleItem?.Image,
                Spotify = categorisedItem.ResolvedSpotifyItem?.Image,
                YouTube = categorisedItem.ResolvedYouTubeItem?.Image,
                Other = categorisedItem.ResolvedNonPodcastServiceItem?.Image
            };
        }

        if (categorisedItem.MatchingPodcast != null && categorisedItem.MatchingPodcast.HasIgnoreAllEpisodes())
        {
            newEpisode.Ignored = true;
        }

        logger.LogInformation(
            $"Created episode with spotify-id '{categorisedItem.ResolvedSpotifyItem?.EpisodeId}', apple-id '{categorisedItem.ResolvedAppleItem?.EpisodeId}', youtube-id '{categorisedItem.ResolvedYouTubeItem?.EpisodeId}' and episode-id '{newEpisode.Id}'.");
        return newEpisode;
    }
}