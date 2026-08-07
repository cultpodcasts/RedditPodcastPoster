
namespace RedditPodcastPoster.Models.Subjects;

public enum SubjectMatchSource
{
    Title = 1,
    Description = 2,
    /// <summary>Subject applied because it is the podcast's DefaultSubject (no title/description term hit).</summary>
    PodcastDefault = 3
}
