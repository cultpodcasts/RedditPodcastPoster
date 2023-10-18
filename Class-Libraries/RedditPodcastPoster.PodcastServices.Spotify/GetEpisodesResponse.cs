using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public record GetEpisodesResponse(IList<Episode>? Results, bool ExpensiveQueryFound=false);