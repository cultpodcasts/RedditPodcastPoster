using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EpisodeRetrievalHandlerResponse(IList<Episode> Episodes, bool Handled);