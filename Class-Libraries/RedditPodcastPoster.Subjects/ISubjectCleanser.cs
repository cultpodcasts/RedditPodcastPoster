namespace RedditPodcastPoster.Subjects;

public interface ISubjectCleanser
{
    Task<(bool, List<string>)> CleanSubjects(List<string> subjects);
}