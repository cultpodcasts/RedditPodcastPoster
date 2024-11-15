namespace RedditPodcastPoster.Indexing;

public record IndexResponse(IndexStatus IndexStatus, IndexedEpisode[]? UpdatedEpisodes = null);