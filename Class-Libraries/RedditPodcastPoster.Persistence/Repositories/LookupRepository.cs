using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Models.YouTubeQuota;
using RedditPodcastPoster.Models.HomePage;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class LookupRepository(
    Container lookupContainer,
    ILogger<LookupRepository> logger)
    : ILookupRepository
{
    public Task<EliminationTerms?> GetEliminationTerms()
    {
        return GetBy<EliminationTerms>(x => x.Id == EliminationTerms._Id && x.ModelType == ModelType.EliminationTerms);
    }

    public Task<DiscoveryScheduleConfig?> GetDiscoveryScheduleConfig()
    {
        return GetById<DiscoveryScheduleConfig>(DiscoveryScheduleConfig._Id);
    }

    public Task<SupportedLanguagesConfig?> GetSupportedLanguagesConfig()
    {
        return GetById<SupportedLanguagesConfig>(SupportedLanguagesConfig._Id);
    }

    public Task<TKnownTerms?> GetKnownTerms<TKnownTerms>() where TKnownTerms : CosmosSelector
    {
        return GetBy<TKnownTerms>(x => x.ModelType == ModelType.KnownTerms);
    }

    public Task<HomePageCache?> GetHomePageCache()
    {
        return GetBy<HomePageCache>(x => x.ModelType == ModelType.HomePageCache);
    }

    public async Task SaveEliminationTerms(EliminationTerms eliminationTerms)
    {
        await lookupContainer.UpsertItemAsync(eliminationTerms, new PartitionKey(eliminationTerms.Id.ToString()));
    }

    public async Task SaveDiscoveryScheduleConfig(DiscoveryScheduleConfig config)
    {
        await lookupContainer.UpsertItemAsync(config, new PartitionKey(config.Id.ToString()));
    }

    public async Task SaveSupportedLanguagesConfig(SupportedLanguagesConfig config)
    {
        await lookupContainer.UpsertItemAsync(config, new PartitionKey(config.Id.ToString()));
    }

    public async Task SaveKnownTerms<TKnownTerms>(TKnownTerms knownTerms) where TKnownTerms : CosmosSelector
    {
        await lookupContainer.UpsertItemAsync(knownTerms, new PartitionKey(knownTerms.Id.ToString()));
    }

    public async Task SaveHomePageCache(HomePageCache homePageCache)
    {
        await lookupContainer.UpsertItemAsync(homePageCache, new PartitionKey(homePageCache.Id.ToString()));
    }

    public async Task SaveYouTubeQuotaReport(YouTubeQuotaReport report)
    {
        await lookupContainer.UpsertItemAsync(report, new PartitionKey(report.Id.ToString()));
    }

    public Task<YouTubeQuotaReport?> GetYouTubeQuotaReport()
    {
        return GetById<YouTubeQuotaReport>(YouTubeQuotaReport._Id);
    }

    public Task<YouTubeIndexerKeyState?> GetYouTubeIndexerKeyState()
    {
        return GetById<YouTubeIndexerKeyState>(YouTubeIndexerKeyState._Id);
    }

    public async Task SaveYouTubeIndexerKeyState(YouTubeIndexerKeyState state)
    {
        await lookupContainer.UpsertItemAsync(state, new PartitionKey(state.Id.ToString()));
    }

    public Task<YouTubeQuotaUsageState?> GetYouTubeQuotaUsageState()
    {
        return GetById<YouTubeQuotaUsageState>(YouTubeQuotaUsageState._Id);
    }

    public async Task SaveYouTubeQuotaUsageState(YouTubeQuotaUsageState state)
    {
        await lookupContainer.UpsertItemAsync(state, new PartitionKey(state.Id.ToString()));
    }

    public async IAsyncEnumerable<string> GetAllDocumentJson(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var iterator = lookupContainer.GetItemQueryStreamIterator(
            new QueryDefinition("SELECT * FROM c"),
            requestOptions: new QueryRequestOptions { MaxItemCount = 100 });

        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "{method}: LookUps query failed with status {Status} ({Error}).",
                    nameof(GetAllDocumentJson),
                    response.StatusCode,
                    response.ErrorMessage);
                throw new InvalidOperationException(
                    $"LookUps container query failed: {(int)response.StatusCode} {response.ErrorMessage}");
            }

            using var document = await JsonDocument.ParseAsync(response.Content, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("Documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in documents.EnumerateArray())
            {
                yield return item.GetRawText();
            }
        }
    }

    public async Task IncrementHomePageActiveEpisodeCount(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        try
        {
            await lookupContainer.PatchItemAsync<HomePageCache>(
                HomePageCache._Id.ToString(),
                new PartitionKey(HomePageCache._Id.ToString()),
                [PatchOperation.Increment("/activeEpisodeCount", delta)]);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogDebug(ex,
                "{MethodName}: HomePageCache document not found while incrementing active episode count by {Delta}.",
                nameof(IncrementHomePageActiveEpisodeCount),
                delta);
        }
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
