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

    public Task<ETagged<NewsReport>?> GetNewsReportWithETag(Guid newsOrganisationId, Guid reportId) =>
        CataloguePlayableFieldPatchOperations.ReadWithETag<NewsReport>(
            container, reportId.ToString(), ToPartitionKey(newsOrganisationId));

    public Task<CataloguePlayablePatchOutcome> PatchFields(
        Guid newsOrganisationId,
        Guid reportId,
        string ifMatchETag,
        CataloguePlayableFieldPatch patch) =>
        CataloguePlayableFieldPatchOperations.Apply(
            container, reportId.ToString(), ToPartitionKey(newsOrganisationId), ifMatchETag, patch);

    public IAsyncEnumerable<NewsReport> GetAll() => GetAllBy(_ => true);

    public Task<int> Count()
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        return CatalogueFeed.Count(
            iterator,
            ex => logger.LogError(ex, "{method}: error counting news reports.", nameof(Count)));
    }

    public Task<int> Count(Guid newsOrganisationId)
    {
        var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.newsOrganisationId = @orgId")
                .WithParameter("@orgId", newsOrganisationId.ToString()),
            requestOptions: new QueryRequestOptions { PartitionKey = ToPartitionKey(newsOrganisationId) });

        return CatalogueFeed.Count(
            iterator,
            ex => logger.LogError(
                ex,
                "{method}: error counting news reports by org-id '{OrgId}'.",
                nameof(Count),
                newsOrganisationId));
    }

    public IAsyncEnumerable<NewsReport> GetByNewsOrganisationId(Guid newsOrganisationId)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = ToPartitionKey(newsOrganisationId)
            })
            .Where(x => x.NewsOrganisationId == newsOrganisationId);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving news reports.", nameof(GetByNewsOrganisationId)));
    }

    public IAsyncEnumerable<NewsReport> GetByNewsOrganisationId(
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

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(
                ex,
                "{method}: error retrieving filtered news reports.",
                nameof(GetByNewsOrganisationId)));
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
        try
        {
            await container.DeleteItemAsync<NewsReport>(reportId.ToString(), ToPartitionKey(newsOrganisationId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Idempotent (as NewsOrganisationRepository.Delete): a row already gone, e.g. a throttled delete that
            // landed before its retry, is the outcome the caller asked for.
        }
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

    public IAsyncEnumerable<NewsReport> GetAllBy(Expression<Func<NewsReport, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions())
            .Where(selector);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving news reports with selector.", nameof(GetAllBy)));
    }

    public IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<NewsReport, bool>> selector,
        Expression<Func<NewsReport, TProjection>> projection)
    {
        var query = container
            .GetItemLinqQueryable<NewsReport>(requestOptions: new QueryRequestOptions())
            .Where(selector)
            .Select(projection);

        return CatalogueFeed.Read(
            query.ToFeedIterator(),
            ex => logger.LogError(ex, "{method}: error retrieving projected news reports.", nameof(GetAllBy)));
    }
}
