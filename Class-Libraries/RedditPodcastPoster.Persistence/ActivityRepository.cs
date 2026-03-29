using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using Activity = RedditPodcastPoster.Models.Activity;

namespace RedditPodcastPoster.Persistence;

public class ActivityRepository(
    Container activitiesContainer,
    ILogger<ActivityRepository> logger)
    : IActivityRepository
{
    public async Task Save(Activity activity)
    {
        await activitiesContainer.UpsertItemAsync(activity, new PartitionKey(activity.Id.ToString()));
    }

    public async Task<Activity?> Get(Guid activityId)
    {
        try
        {
            return await activitiesContainer.ReadItemAsync<Activity>(activityId.ToString(),
                new PartitionKey(activityId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task Delete(Guid activityId)
    {
        try
        {
            await activitiesContainer.DeleteItemAsync<Activity>(activityId.ToString(), new PartitionKey(activityId.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // idempotent delete
        }
    }

    public async IAsyncEnumerable<Activity> GetAllBy(Expression<Func<Activity, bool>> selector)
    {
        var query = activitiesContainer
            .GetItemLinqQueryable<Activity>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<Activity> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving activities.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
