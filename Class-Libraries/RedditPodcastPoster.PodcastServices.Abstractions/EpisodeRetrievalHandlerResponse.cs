
namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EpisodeRetrievalHandlerResponse(IList<Models.V2.Episode> Episodes, bool Handled);