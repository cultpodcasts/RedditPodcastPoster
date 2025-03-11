namespace RedditPodcastPoster.Indexing.Models;

public record IndexResponse(IndexStatus IndexStatus, IndexedEpisode[]? UpdatedEpisodes = null);