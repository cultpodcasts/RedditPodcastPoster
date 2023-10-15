using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record GetEpisodesResponse(IList<Episode>? Results, bool ExpensiveQueryFound=false);