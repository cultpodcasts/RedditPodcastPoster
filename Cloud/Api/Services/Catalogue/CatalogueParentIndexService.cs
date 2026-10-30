using Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Catalogue;

/// <summary>
/// A curator's "Index" on a TV show or news organisation hub page, the counterpart of <c>PodcastIndexService</c>.
/// <list type="number">
/// <item>Resolve the parent by guid or by a unique name (case-insensitive, as rename does): none is NotFound, more
/// than one is Conflict with the candidate ids.</item>
/// <item>Index it now with <see cref="ICatalogueParentsUpdater.IndexParent"/> (the hourly indexer's discovery, merge,
/// filters, time budget and indexer-state patch) using the curator indexing context: every resolver and expensive
/// query on, no YouTube indexing hints.</item>
/// <item>Push the saved items to search straight away (<see cref="CataloguePlayableSearchSync.AfterIndex"/>).</item>
/// </list>
/// Like the podcast index it can run at any time, including during the hourly pass.
/// </summary>
public class CatalogueParentIndexService(
    ITvShowRepository tvShowRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ICatalogueParentsUpdater catalogueParentsUpdater,
    CataloguePlayableSearchSync searchSync,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<CatalogueParentIndexService> logger) : ICatalogueParentIndexService
{
    public Task<CatalogueIndexResult> IndexTvShow(string identifier, CancellationToken cancellationToken) =>
        Index("TV show", tvShowRepository, tvShowRepository.GetTvShow, identifier, cancellationToken);

    public Task<CatalogueIndexResult> IndexNewsOrganisation(string identifier, CancellationToken cancellationToken) =>
        Index("news organisation", newsOrganisationRepository, newsOrganisationRepository.GetNewsOrganisation,
            identifier, cancellationToken);

    private async Task<CatalogueIndexResult> Index<TEntity>(
        string kind,
        IFilterableRepository<TEntity> repository,
        Func<Guid, Task<TEntity?>> getById,
        string identifier,
        CancellationToken cancellationToken) where TEntity : Publisher
    {
        try
        {
            TEntity? entity;
            if (Guid.TryParse(identifier, out var id))
            {
                entity = await getById(id);
            }
            else
            {
                var matches = await PublisherNameAttachLookup.FindByName(
                    repository, PodcastRouteNameNormalizer.Normalize(identifier), cancellationToken);
                if (matches.Count > 1)
                {
                    return new CatalogueIndexResult(CatalogueIndexStatus.Conflict,
                        AmbiguousIds: matches.Select(x => x.Id).ToArray());
                }

                entity = matches.SingleOrDefault();
            }

            if (entity is null)
            {
                logger.LogWarning("Index {kind}: not found with identifier '{identifier}'.", kind, identifier);
                return new CatalogueIndexResult(CatalogueIndexStatus.NotFound);
            }

            logger.LogInformation("Index {kind} '{name}' ({id}).", kind, entity.Name, entity.Id);
            // The parent runs under the indexer's per-parent time budget (its own token), as in the hourly pass.
            var result = await catalogueParentsUpdater.IndexParent(
                entity.Id, CuratorIndexingContext.Create(indexerOptions.Value));
            switch (result.Status)
            {
                case CatalogueParentIndexStatus.Indexed:
                    var search = await searchSync.AfterIndex(entity.Name, result.Changed, cancellationToken);
                    return new CatalogueIndexResult(CatalogueIndexStatus.Ok,
                        result.Changed.Select(x => x.Id).ToArray(), search);
                case CatalogueParentIndexStatus.NotFound:
                    return new CatalogueIndexResult(CatalogueIndexStatus.NotFound);
                case CatalogueParentIndexStatus.NotIndexable:
                    return new CatalogueIndexResult(CatalogueIndexStatus.NotIndexable);
                case CatalogueParentIndexStatus.SourcePodcastStillExists:
                    return new CatalogueIndexResult(CatalogueIndexStatus.SourcePodcastStillExists);
                case CatalogueParentIndexStatus.BudgetExceeded:
                    return new CatalogueIndexResult(CatalogueIndexStatus.BudgetExceeded);
                default:
                    logger.LogError("Index {kind}: unexpected status '{status}'.", kind, result.Status);
                    return new CatalogueIndexResult(CatalogueIndexStatus.Failed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Index {kind} failed for identifier '{identifier}'.", kind, identifier);
            return new CatalogueIndexResult(CatalogueIndexStatus.Failed);
        }
    }
}
