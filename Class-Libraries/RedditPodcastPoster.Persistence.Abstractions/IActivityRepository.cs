using System.Linq.Expressions;
using Activity = RedditPodcastPoster.Models.Activity;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IActivityRepository
{
    Task Save(Activity activity);
    Task<Activity?> Get(Guid activityId);
    Task Delete(Guid activityId);
    IAsyncEnumerable<Activity> GetAllBy(Expression<Func<Activity, bool>> selector);
}
