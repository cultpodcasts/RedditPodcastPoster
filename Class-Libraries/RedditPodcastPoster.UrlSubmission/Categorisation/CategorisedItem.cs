using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public record CategorisedItem(
    Podcast? MatchingPodcast,
    Episode? MatchingEpisode,
    ResolvedSpotifyItem? ResolvedSpotifyItem,
    ResolvedAppleItem? ResolvedAppleItem,
    ResolvedYouTubeItem? ResolvedYouTubeItem,
    ResolvedNonPodcastServiceItem? ResolvedNonPodcastServiceItem,
    Service Authority);