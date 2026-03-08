using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class PushSubscriptionRepositoryV2(
    Container pushSubscriptionsContainer,
    ILogger<PushSubscriptionRepositoryV2> logger)
    : IPushSubscriptionRepositoryV2
{
    public async Task Save(PushSubscription pushSubscription)
    {
        await pushSubscriptionsContainer.UpsertItemAsync(pushSubscription,
            new PartitionKey(pushSubscription.Id.ToString()));
    }

    public async IAsyncEnumerable<PushSubscription> GetAll()
    {
        var query = pushSubscriptionsContainer.GetItemLinqQueryable<PushSubscription>(requestOptions: new QueryRequestOptions());
        var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            FeedResponse<PushSubscription> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving push subscriptions.", nameof(GetAll));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task Delete(PushSubscription pushSubscription)
    {
        try
        {
            await pushSubscriptionsContainer.DeleteItemAsync<PushSubscription>(
                pushSubscription.Id.ToString(),
                new PartitionKey(pushSubscription.Id.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // idempotent delete
        }
    }
}
