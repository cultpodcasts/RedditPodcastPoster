namespace RedditPodcastPoster.PodcastServices.Apple;

public record FindApplePodcastRequest(
    long? PodcastAppleId,
    string PodcastName,
    string PodcastPublisher);