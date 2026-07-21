using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.HashTags;

public interface IHashTagProvider
{
    Task<ICollection<HashTag>> GetHashTags(List<string> episodeSubjects);
}