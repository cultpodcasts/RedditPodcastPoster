namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public record YouTubeGetEpisodesRequest(string YouTubeChannelId, DateTime? ProcessRequestReleasedSince);