namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public record YouTubeGetEpisodesRequest(string YouTubeChannelId, DateTime? ProcessRequestReleasedSince);
public record YouTubeGetPlaylistEpisodesRequest(string playlistId, DateTime? ProcessRequestReleasedSince);