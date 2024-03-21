namespace RedditPodcastPoster.Twitter;

public interface IHashTagProvider
{
    Task<ICollection<(string HashTag, string? EnrichmentHashTag)>> GetHashTags(List<string> episodeSubjects);
}