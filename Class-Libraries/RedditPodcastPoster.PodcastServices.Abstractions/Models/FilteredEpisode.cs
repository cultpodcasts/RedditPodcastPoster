using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record FilteredEpisode(Episode Episode, string[] Terms);