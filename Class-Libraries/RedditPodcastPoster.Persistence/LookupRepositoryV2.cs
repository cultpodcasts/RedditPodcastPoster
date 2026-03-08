using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class LookupRepositoryV2(
    Container lookupContainer,
    ILogger<LookupRepositoryV2> logger)
    : ILookupRepositoryV2
{
    public Task<EliminationTerms?> GetEliminationTerms()
    {
        return GetBy<EliminationTerms>(x => x.Id == EliminationTerms._Id && x.ModelType == ModelType.EliminationTerms);
    }

    public Task<TKnownTerms?> GetKnownTerms<TKnownTerms>() where TKnownTerms : CosmosSelector
    {
        return GetBy<TKnownTerms>(x => x.ModelType == ModelType.KnownTerms);
    }

    private async Task<T?> GetById<T>(Guid id) where T : CosmosSelector
    {
        try
        {
            return await lookupContainer.ReadItemAsync<T>(id.ToString(), new PartitionKey(id.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<T?> GetBy<T>(System.Linq.Expressions.Expression<Func<T, bool>> selector)
        where T : CosmosSelector
    {
        var query = lookupContainer
            .GetItemLinqQueryable<T>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            try
            {
                foreach (var item in await iterator.ReadNextAsync())
                {
                    return item;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving lookup document.", nameof(GetBy));
                throw;
            }
        }

        return null;
    }
}
