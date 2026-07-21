using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public interface ISubjectsPublisher
{
    Task PublishSubjects();
    Task PublishFlairs();
}
