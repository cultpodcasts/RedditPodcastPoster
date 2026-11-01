using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <param name="CategorisedUrl">The discovery URL the item was categorised from (its classification signals' URL).</param>
public record CreateCategorisedItemResponse(
    CategorisedItem CategorisedItem,
    Uri CategorisedUrl,
    bool EnrichSpotify,
    bool EnrichApple,
    bool EnrichYouTube);