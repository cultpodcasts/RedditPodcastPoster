using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public record GetPlaylistEpisodesResponse(IList<Episode>? Results, bool IsExpensiveQuery=false);