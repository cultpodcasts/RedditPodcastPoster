namespace RedditPodcastPoster.ContentPublisher;

public interface IContentPublisher
{
    Task PublishHomepage();
    Task PublishSubjects();
    Task PublishFlairs();
}