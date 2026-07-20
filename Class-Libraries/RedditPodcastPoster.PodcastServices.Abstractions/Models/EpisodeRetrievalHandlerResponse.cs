
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record EpisodeRetrievalHandlerResponse(IList<Episode> Episodes, bool Handled);