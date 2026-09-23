using System.Linq.Expressions;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class NewsReportRepository(
    Container container,
    ILogger<NewsReportRepository> logger)
    : INewsReportRepository
{
    private static PartitionKey ToPartitionKey(Guid newsOrganisationId) => new(newsOrganisationId.ToString());

    public async Task<NewsReport?> GetNewsReport(Guid newsOrganisationId, Guid reportId)
    {
        try
        {
            return await container.ReadItemAsync<NewsReport>(reportId.ToString(), ToPartitionKey(newsOrganisationId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public IAsyncEnumerable<NewsReport> GetAll() => GetAllBy(_ => true);

    public async Task<int> Count()
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        while (iterator.HasMoreResults)
        {
            try
            {
                foreach (var count in await iterator.ReadNextAsync())
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error counting news reports.", nameof(Count));
                throw;
            }
        }

        return 0;
    }

    public async Task<int> Count(Guid newsOrganisationId)
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.newsOrganisationId = @orgId")
                .WithParameter("@orgId", newsOrganisationId.ToString()),
            requestOptions: new QueryRequestOptions { PartitionKey = ToPartitionKey(newsOrganisationId) });

        while (iterator.HasMoreResults)
        {
            try
            {
                foreach (var count in await iterator.ReadNextAsync())
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error counting news reports by org-id '{OrgId}'.",
                    nameof(Count), newsOrganisationId);
                throw;
            }
        }

        return 0;
    }

    public async IAsyncEnumerable<NewsReport> GetByNewsOrganisationId(Guid newsOrganisationId)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(newsOrganisationId)
            })
            .Where(x => x.NewsOrganisationId == newsOrganisationId);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<NewsReport> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving news reports.", nameof(GetByNewsOrganisationId));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<NewsReport> GetByNewsOrganisationId(
        Guid newsOrganisationId,
        Expression<Func<NewsReport, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(newsOrganisationId)
            })
            .Where(x => x.NewsOrganisationId == newsOrganisationId)
            .Where(selector);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<NewsReport> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving filtered news reports.",
                    nameof(GetByNewsOrganisationId));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task Save(NewsReport report)
    {
        if (report.Id == Guid.Empty || report.NewsOrganisationId == Guid.Empty)
        {
            throw new ArgumentException("NewsReport Id and NewsOrganisationId must be set before save.");
        }

        await container.UpsertItemAsync(report, ToPartitionKey(report.NewsOrganisationId));
    }

    public async Task Save(IEnumerable<NewsReport> reports)
    {
        foreach (var report in reports)
        {
            await Save(report);
        }
    }

    public async Task Delete(Guid newsOrganisationId, Guid reportId)
    {
        await container.DeleteItemAsync<NewsReport>(reportId.ToString(), ToPartitionKey(newsOrganisationId));
    }

    public async Task<NewsReport?> GetBy(Expression<Func<NewsReport, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                return item;
            }
        }

        return null;
    }

    public async IAsyncEnumerable<NewsReport> GetAllBy(Expression<Func<NewsReport, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<NewsReport> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving news reports with selector.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<NewsReport, bool>> selector,
        Expression<Func<NewsReport, TProjection>> projection)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<TProjection> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving projected news reports.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
