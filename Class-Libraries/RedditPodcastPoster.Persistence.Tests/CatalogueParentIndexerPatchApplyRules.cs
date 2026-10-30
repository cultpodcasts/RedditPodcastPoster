using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1085: how the indexer patch is sent to Cosmos. A cleared member is a filtered single-op Remove; a failed filter
/// (412) means "already absent" and is the only failure that may be ignored.
/// </summary>
public class CatalogueParentIndexerPatchApplyRules : IDisposable
{
    private readonly List<(IReadOnlyList<PatchOperation> Chunk, string? Filter)> _sent = [];
    private readonly CosmosClient _client = new(
        "https://synthetic.documents.example:443/",
        Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
        new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
        });

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private Func<string?, HttpStatusCode?> _failureFor = _ => null;

    public CatalogueParentIndexerPatchApplyRules()
    {
        _mocker.GetMock<Database>().Setup(x => x.Client).Returns(() => _client);
        _mocker.GetMock<Container>().Setup(x => x.Database).Returns(() => _mocker.Get<Database>());
        _mocker.GetMock<Container>()
            .Setup(x => x.PatchItemAsync<JsonObject>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<IReadOnlyList<PatchOperation>>(), It.IsAny<PatchItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, PartitionKey _, IReadOnlyList<PatchOperation> chunk, PatchItemRequestOptions options,
                CancellationToken _) =>
            {
                _sent.Add((chunk, options.FilterPredicate));
                return _failureFor(options.FilterPredicate) is { } status
                    ? Task.FromException<ItemResponse<JsonObject>>(
                        new CosmosException("synthetic", status, 0, string.Empty, 0))
                    : Task.FromResult<ItemResponse<JsonObject>>(null!);
            });
    }

    public void Dispose() => _client.Dispose();

    private CatalogueParentIndexerPatch CreatePatchWithTwoRemovalsAndASet()
    {
        var indexing = CatalogueIndexingSource.FromPodcast(_fixture.CreatePodcast());
        indexing.YouTubePlaylistId = null;
        indexing.SpotifyEpisodeCount = null;
        indexing.AppleEpisodeCount = 7;
        return new CatalogueParentIndexerPatch(
            DomainTestFixture.UtcDaysAgo(0), null, indexing,
            ["youTubePlaylistId", "spotifyEpisodeCount", "appleEpisodeCount"]);
    }

    [Fact(DisplayName =
        "Indexer patch apply: a filtered removal whose member is already absent (412) counts as done and the rest of " +
        "the patch is still applied, because the removal wanted the member gone and it is (#1085).")]
    public async Task already_absent_removal_counts_as_done()
    {
        // Arrange
        _failureFor = filter => filter != null ? HttpStatusCode.PreconditionFailed : null;
        var sut = _mocker.CreateInstance<TvShowRepository>();

        // Act
        var applied = await sut.PatchIndexerState(_fixture.CreateGuid(), CreatePatchWithTwoRemovalsAndASet());

        // Assert
        applied.Should().BeTrue();
        _sent.Should().HaveCount(3);
        _sent.Should().OnlyContain(x => x.Filter == CatalogueParentIndexerPatchOperations.FilterPredicateFor(x.Chunk));
        _sent.Take(2).Should().OnlyContain(x => x.Filter != null && x.Chunk.Single().OperationType == PatchOperationType.Remove);
        _sent[^1].Chunk.Select(x => x.Path).Should().Equal("/indexing/appleEpisodeCount", "/lastIndexed");
    }

    [Fact(DisplayName =
        "Indexer patch apply: a 412 on an unfiltered chunk of sets is surfaced, because only a filtered removal's " +
        "precondition means 'already absent' and anything else is a real failure (#1085).")]
    public async Task precondition_failure_on_sets_propagates()
    {
        // Arrange
        _failureFor = filter => filter == null ? HttpStatusCode.PreconditionFailed : null;
        var sut = _mocker.CreateInstance<NewsOrganisationRepository>();

        // Act
        var act = () => sut.PatchIndexerState(_fixture.CreateGuid(), CreatePatchWithTwoRemovalsAndASet());

        // Assert
        (await act.Should().ThrowAsync<CosmosException>()).Which.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact(DisplayName =
        "Indexer patch apply: any other failure of a removal is surfaced and /lastIndexed is not written, because a " +
        "throttled or failed removal is not 'already absent' and the pass must not be recorded as completed (#1085).")]
    public async Task other_removal_failure_propagates_before_last_indexed()
    {
        // Arrange
        _failureFor = filter => filter != null ? HttpStatusCode.TooManyRequests : null;
        var sut = _mocker.CreateInstance<TvShowRepository>();

        // Act
        var act = () => sut.PatchIndexerState(_fixture.CreateGuid(), CreatePatchWithTwoRemovalsAndASet());

        // Assert
        (await act.Should().ThrowAsync<CosmosException>()).Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        _sent.SelectMany(x => x.Chunk).Should().NotContain(x => x.Path == "/lastIndexed");
    }

    [Fact(DisplayName =
        "Indexer patch apply: a parent deleted mid-pass (404) returns false, because the caller must know nothing " +
        "was written.")]
    public async Task deleted_parent_returns_false()
    {
        // Arrange
        _failureFor = _ => HttpStatusCode.NotFound;
        var sut = _mocker.CreateInstance<TvShowRepository>();

        // Act
        var applied = await sut.PatchIndexerState(_fixture.CreateGuid(), CreatePatchWithTwoRemovalsAndASet());

        // Assert
        applied.Should().BeFalse();
    }
}
