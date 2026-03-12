namespace RedditPodcastPoster.ContentPublisher;

public interface ISubjectsPublisher
{
    Task PublishSubjects();
    Task PublishFlairs();
}
