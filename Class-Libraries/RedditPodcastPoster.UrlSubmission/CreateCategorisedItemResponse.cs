using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public record CreateCategorisedItemResponse(
    CategorisedItem CategorisedItem,
    bool EnrichSpotify,
    bool EnrichApple,
    bool EnrichYouTube);