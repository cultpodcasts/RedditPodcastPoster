namespace KVWriter.Shortner;

public record ShortUrlRecord(string PodcastName, Guid EpisodeId, string Base64EpisodeKey);