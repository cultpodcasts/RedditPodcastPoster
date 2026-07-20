namespace RedditPodcastPoster.PodcastServices.Apple.Models;

public record FindApplePodcastRequest(
    long? PodcastAppleId,
    string PodcastName,
    string PodcastPublisher);