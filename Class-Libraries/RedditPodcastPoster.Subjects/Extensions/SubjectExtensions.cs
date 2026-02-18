using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class SubjectExtensions
{
    extension(Subject subject)
    {
        public bool IsVisible => !subject.Name.StartsWith("_");
    }
}