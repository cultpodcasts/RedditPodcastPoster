using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record CreateCategorisedItemResponse(
    CategorisedItem CategorisedItem,
    bool EnrichSpotify,
    bool EnrichApple,
    bool EnrichYouTube);