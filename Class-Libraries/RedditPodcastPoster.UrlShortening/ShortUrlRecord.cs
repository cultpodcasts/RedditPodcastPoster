namespace RedditPodcastPoster.UrlShortening;

public record ShortUrlRecord(string PodcastName, Guid EpisodeId, string Base64EpisodeKey);