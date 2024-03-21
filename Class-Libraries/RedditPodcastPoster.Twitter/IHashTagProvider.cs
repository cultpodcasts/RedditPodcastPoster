namespace RedditPodcastPoster.Twitter;

public interface IHashTagProvider
{
    Task<ICollection<HashTag>> GetHashTags(List<string> episodeSubjects);
}