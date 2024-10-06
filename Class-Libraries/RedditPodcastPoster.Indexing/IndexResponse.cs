namespace RedditPodcastPoster.Indexing;

public record IndexResponse(IndexStatus IndexStatus, Guid[]? UpdatedEpisodeIds = null);