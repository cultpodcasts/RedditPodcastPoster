
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

public record GetEpisodesResponse(IList<Episode>? Results, bool ExpensiveQueryFound=false);