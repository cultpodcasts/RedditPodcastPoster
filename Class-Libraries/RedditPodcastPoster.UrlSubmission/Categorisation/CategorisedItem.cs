using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public record CategorisedItem(
    Podcast? MatchingPodcast,
    IEnumerable<Episode>? MatchingPodcastEpisodes,
    Episode? MatchingEpisode,
    ResolvedSpotifyItem? ResolvedSpotifyItem,
    ResolvedAppleItem? ResolvedAppleItem,
    ResolvedYouTubeItem? ResolvedYouTubeItem,
    ResolvedNonPodcastServiceItem? ResolvedNonPodcastServiceItem,
    Service Authority);