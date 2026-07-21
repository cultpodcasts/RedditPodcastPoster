using System.Linq.Expressions;
using Activity = RedditPodcastPoster.Models.Operations.Activity;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IActivityRepository
{
    Task Save(Activity activity);
    Task<Activity?> Get(Guid activityId);
    Task Delete(Guid activityId);
    IAsyncEnumerable<Activity> GetAllBy(Expression<Func<Activity, bool>> selector);
}
