using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using AzureSearchDocument = Azure.Search.Documents.Models.SearchDocument;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

public sealed class FilmSearchIndexGateway(
    SearchClient searchClient,
    ILogger<FilmSearchIndexGateway> logger) : IFilmSearchIndexGateway
{
    internal const string FilmFilter = $"contentKind eq '{SearchContentKind.Film}'";
    private const int IdPageSize = 1000;

    public async Task<long> CountFilmDocuments(CancellationToken cancellationToken)
    {
        var options = new SearchOptions { Filter = FilmFilter, Size = 0, IncludeTotalCount = true };
        var response = await searchClient.SearchAsync<AzureSearchDocument>("*", options, cancellationToken);
        return response.Value.TotalCount ?? 0;
    }

    public async Task<IReadOnlyList<string>> GetFilmDocumentIds(CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        while (true)
        {
            var options = new SearchOptions { Filter = FilmFilter, Size = IdPageSize, Skip = ids.Count };
            options.Select.Add("id");
            var response = await searchClient.SearchAsync<AzureSearchDocument>("*", options, cancellationToken);
            var page = 0;
            await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
            {
                if (result.Document.TryGetValue("id", out var id) && id is string value)
                {
                    ids.Add(value);
                }

                page++;
            }

            if (page < IdPageSize)
            {
                return ids;
            }
        }
    }

    public async Task<IReadOnlyList<string>> MergeOrUpload(
        IReadOnlyList<EpisodeSearchRecord> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        var result = await searchClient.MergeOrUploadDocumentsAsync(
            documents, new IndexDocumentsOptions { ThrowOnAnyError = false }, cancellationToken);
        return Failures(result.Value, "push");
    }

    public async Task<IReadOnlyList<string>> Delete(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var result = await searchClient.DeleteDocumentsAsync(
            "id", ids, new IndexDocumentsOptions { ThrowOnAnyError = false }, cancellationToken);
        return Failures(result.Value, "delete");
    }

    private IReadOnlyList<string> Failures(IndexDocumentsResult result, string action)
    {
        var failed = new List<string>();
        foreach (var item in result.Results.Where(x => !x.Succeeded))
        {
            logger.LogError(
                "Film search {Action} failed for document '{Key}' (HTTP {Status}): {ErrorMessage}",
                action, item.Key, item.Status, item.ErrorMessage);
            failed.Add(item.Key);
        }

        return failed;
    }
}
