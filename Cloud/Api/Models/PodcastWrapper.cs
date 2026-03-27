using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace Api.Models;

public record PodcastWrapper(
    Podcast? Podcast,
    PodcastRetrievalState RetrievalState,
    IEnumerable<Guid>? AmbiguousPodcasts = null);