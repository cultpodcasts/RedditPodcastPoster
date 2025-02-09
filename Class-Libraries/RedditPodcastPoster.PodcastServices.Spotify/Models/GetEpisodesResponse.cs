using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public record GetEpisodesResponse(IList<Episode>? Results, bool ExpensiveQueryFound=false);