using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public record CategorisedItem(
    Podcast? MatchingPodcast,
    IEnumerable<Episode>? MatchingPodcastEpisodes,
    Episode? MatchingEpisode,
    CategorisedSpotifyItem? ResolvedSpotifyItem,
    CategorisedAppleItem? ResolvedAppleItem,
    CategorisedYouTubeItem? ResolvedYouTubeItem,
    ResolvedNonPodcastServiceItem? ResolvedNonPodcastServiceItem,
    Service Authority);
