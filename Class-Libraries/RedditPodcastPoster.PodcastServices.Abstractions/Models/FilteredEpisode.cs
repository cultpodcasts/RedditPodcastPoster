using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record FilteredEpisode(Episode Episode, string[] Terms);