using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public record CategorisedItem(
    Podcast? MatchingPodcast,
    Episode? MatchingEpisode,
    ResolvedSpotifyItem? ResolvedSpotifyItem, 
    ResolvedAppleItem? ResolvedAppleItem,
    ResolvedYouTubeItem? ResolvedYouTubeItem,
    Service Authority);
