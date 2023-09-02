namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record FindApplePodcastRequest(
    long? PodcastAppleId,
    string PodcastName,
    string PodcastPublisher);