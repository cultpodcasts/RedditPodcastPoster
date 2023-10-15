using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public record EpisodeRetrievalHandlerResponse(IList<Episode> Episodes, bool Handled);