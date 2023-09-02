namespace RedditPodcastPoster.Common.UrlCategorisation;

public record CategorisedItem(
    ResolvedSpotifyItem? ResolvedSpotifyItem, 
    ResolvedAppleItem? ResolvedAppleItem,
    ResolvedYouTubeItem? ResolvedYouTubeItem);
