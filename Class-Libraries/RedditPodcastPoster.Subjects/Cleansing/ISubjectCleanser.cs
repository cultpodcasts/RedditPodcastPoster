namespace RedditPodcastPoster.Subjects.Cleansing;

public interface ISubjectCleanser
{
    Task<(bool, List<string>)> CleanSubjects(List<string> subjects);
}