using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record FilteredEpisode(Episode Episode, string[] Terms);