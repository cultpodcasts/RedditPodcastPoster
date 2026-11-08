using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

public class PlayableSearchDocumentDeleteRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Mock<SearchClient> _client = new();
    private readonly List<string[]> _batches = [];

    private PlayableSearchDocumentSwap Create(bool succeeds)
    {
        _client
            .Setup(c => c.DeleteDocumentsAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IndexDocumentsOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<string>, IndexDocumentsOptions, CancellationToken>(
                (_, keys, _, _) => _batches.Add(keys.ToArray()))
            .ReturnsAsync(Response.FromValue(
                SearchModelFactory.IndexDocumentsResult(
                    [SearchModelFactory.IndexingResult("id", succeeds ? null : "synthetic failure", succeeds, succeeds ? 200 : 503)]),
                Mock.Of<Response>()));
        return new PlayableSearchDocumentSwap(_client.Object, NullLogger<PlayableSearchDocumentSwap>.Instance);
    }

    [Fact(DisplayName =
        "Deleting search documents sends their ids and reports success only when every delete succeeded.")]
    public async Task delete_reports_per_document_outcome()
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var succeeded = await Create(succeeds: true).DeleteAsync([id], CancellationToken.None);
        var failed = await Create(succeeds: false).DeleteAsync([id], CancellationToken.None);

        // Assert
        succeeded.Should().BeTrue();
        failed.Should().BeFalse();
        _batches.Should().AllSatisfy(batch => batch.Should().Equal(id.ToString()));
    }

    [Fact(DisplayName =
        "A thrown search error is reported as a failed delete, so the caller keeps the source row.")]
    public async Task delete_exception_is_failure()
    {
        // Arrange
        _client
            .Setup(c => c.DeleteDocumentsAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IndexDocumentsOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(_fixture.CreateTitle()));
        var swap = new PlayableSearchDocumentSwap(_client.Object, NullLogger<PlayableSearchDocumentSwap>.Instance);

        // Act
        var result = await swap.DeleteAsync([_fixture.CreateGuid()], CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A whole podcast transfer's search documents go in batches of at most MaxDocumentsPerBatch (Azure AI Search " +
        "takes at most 1000 per request), and a failed batch does not stop the rest but reports failure.")]
    public async Task upload_and_delete_are_batched()
    {
        // Arrange
        var uploadBatches = new List<int>();
        var call = 0;
        _client
            .Setup(c => c.MergeOrUploadDocumentsAsync(
                It.IsAny<IEnumerable<EpisodeSearchRecord>>(), It.IsAny<IndexDocumentsOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EpisodeSearchRecord>, IndexDocumentsOptions, CancellationToken>(
                (documents, _, _) => uploadBatches.Add(documents.Count()))
            .ReturnsAsync(() =>
            {
                var succeeds = Interlocked.Increment(ref call) != 2;
                return Response.FromValue(
                    SearchModelFactory.IndexDocumentsResult(
                        [SearchModelFactory.IndexingResult("id", succeeds ? null : "synthetic failure", succeeds, succeeds ? 200 : 503)]),
                    Mock.Of<Response>());
            });
        var documents = Enumerable.Range(0, 1200)
            .Select(_ => new EpisodeSearchRecord
            {
                Id = _fixture.CreateGuid().ToString(),
                Release = DateTimeOffset.UnixEpoch,
                Duration = string.Empty,
                BBC = string.Empty,
                InternetArchive = string.Empty,
                Subjects = [],
                PublisherSearchTerms = string.Empty,
                EpisodeSearchTerms = string.Empty
            })
            .ToList();
        var ids = Enumerable.Range(0, 1001).Select(_ => _fixture.CreateGuid()).ToList();
        var swap = Create(succeeds: true);

        // Act
        var uploaded = await swap.UploadAsync(documents, CancellationToken.None);
        var deleted = await swap.DeleteAsync(ids, CancellationToken.None);

        // Assert
        uploaded.Should().BeFalse();
        uploadBatches.Should().Equal(500, 500, 200);
        deleted.Should().BeTrue();
        _batches.Select(batch => batch.Length).Should().Equal(500, 500, 1);
    }
}
