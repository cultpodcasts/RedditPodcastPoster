using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public record CategorisedItem(
    Podcast? MatchingPodcast,
    Episode? MatchingEpisode,
    ResolvedSpotifyItem? ResolvedSpotifyItem, 
    ResolvedAppleItem? ResolvedAppleItem,
    ResolvedYouTubeItem? ResolvedYouTubeItem,
    Service Authority);
