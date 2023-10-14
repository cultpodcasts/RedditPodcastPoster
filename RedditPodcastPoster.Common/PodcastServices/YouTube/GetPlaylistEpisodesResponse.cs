using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public record GetPlaylistEpisodesResponse(IList<Episode>? Results, bool IsExpensiveQuery=false);