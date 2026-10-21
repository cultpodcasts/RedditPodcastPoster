using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FunctionHost.Tests.Api.Services;

/// <summary>A real <see cref="EpisodeSearchIndexCleanup"/> over a mocked Azure Search client that records deletes.</summary>
public sealed class SearchCleanupMock
{
    public Mock<SearchClient> Client { get; } = new();

    public SearchCleanupMock()
    {
        Client
            .Setup(c => c.DeleteDocumentsAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IndexDocumentsOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                SearchModelFactory.IndexDocumentsResult([SearchModelFactory.IndexingResult("id", null, true, 200)]),
                Mock.Of<Response>()));
        Cleanup = new EpisodeSearchIndexCleanup(Client.Object, NullLogger<EpisodeSearchIndexCleanup>.Instance);
    }

    public EpisodeSearchIndexCleanup Cleanup { get; }

    public void VerifyDelete(Guid id, Times times) =>
        Client.Verify(c => c.DeleteDocumentsAsync(
            "id",
            It.Is<IEnumerable<string>>(keys => keys.Single() == id.ToString()),
            It.IsAny<IndexDocumentsOptions>(),
            It.IsAny<CancellationToken>()), times);
}
