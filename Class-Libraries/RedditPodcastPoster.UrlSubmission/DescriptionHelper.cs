using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public class DescriptionHelper(ILogger<DescriptionHelper> logger) : IDescriptionHelper
{
    public string EnrichMissingDescription(CategorisedItem categorisedItem)
    {
        string? altDescription = null;
        switch (categorisedItem.Authority)
        {
            case Service.Spotify:
            {
                altDescription = CollapseDescription(categorisedItem.ResolvedAppleItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedYouTubeItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedNonPodcastServiceItem?.Description);
                break;
            }
            case Service.Apple:
            {
                altDescription = CollapseDescription(categorisedItem.ResolvedSpotifyItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedYouTubeItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedNonPodcastServiceItem?.Description);
                break;
            }
            case Service.YouTube:
            {
                altDescription = CollapseDescription(categorisedItem.ResolvedSpotifyItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedAppleItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedNonPodcastServiceItem?.Description);
                break;
            }
            case Service.Other:
            {
                altDescription = CollapseDescription(categorisedItem.ResolvedSpotifyItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedAppleItem?.EpisodeDescription) ??
                                 CollapseDescription(categorisedItem.ResolvedYouTubeItem?.EpisodeDescription);
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(altDescription))
        {
            return altDescription.Trim();
        }

        return string.Empty;
    }

    public string? CollapseDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return description.Trim();
    }
}