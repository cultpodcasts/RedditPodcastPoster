
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record EpisodeRetrievalHandlerResponse(IList<Episode> Episodes, bool Handled);