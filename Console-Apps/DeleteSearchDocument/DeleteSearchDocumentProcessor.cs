using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace DeleteSearchDocument;

public class DeleteSearchDocumentProcessor(
    SearchClient searchClient,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<DeleteSearchDocumentProcessor> logger)
{
    public async Task Process(DeleteSearchDocumentRequest request)
    {
        IEnumerable<Guid> documentIds;
        if (!request.IsPodcast)
        {
            documentIds = new[] { request.DocumentId };
        }
        else
        {
            var podcast = await podcastRepository.GetBy(x => x.Id == request.DocumentId);
            if (podcast == null)
            {
                throw new ArgumentException($"No podcast found with podcast-id '{request.DocumentId}'.");
            }

            documentIds = await episodeRepository.GetByPodcastId(podcast.Id).Select(x => x.Id).ToArrayAsync();
        }

        foreach (var documentId in documentIds)
        {
            var result = await searchClient.DeleteDocumentsAsync(
                "id",
                [documentId.ToString()],
                new IndexDocumentsOptions { ThrowOnAnyError = true });
            var success = result.Value.Results.First().Succeeded;
            if (!success)
            {
                logger.LogError(result.Value.Results.First().ErrorMessage);
            }
        }
    }
}