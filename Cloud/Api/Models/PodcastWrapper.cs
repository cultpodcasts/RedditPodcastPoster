using RedditPodcastPoster.Models;

namespace Api.Models;

public record PodcastWrapper(Podcast? Podcast, PodcastRetrievalState RetrievalState);