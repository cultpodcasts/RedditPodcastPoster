using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record FilteredEpisode(Episode Episode, string[] Terms);