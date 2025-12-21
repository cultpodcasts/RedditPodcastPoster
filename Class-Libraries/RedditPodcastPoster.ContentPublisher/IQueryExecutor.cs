using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.ContentPublisher;

public interface IQueryExecutor
{
    Task<HomePageModel> GetHomePage(CancellationToken ct);
    SubjectModel GetSubjects();
}