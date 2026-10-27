using System.Net;
using System.Text.Json;
using EpisodeServiceBackfill;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using Xunit;

namespace EpisodeServiceBackfill.Tests;

public class CosmosEpisodeYouTubeIdRepairStoreTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<PatchCall> _patchCalls = [];
    private Exception? _patchException;

    public CosmosEpisodeYouTubeIdRepairStoreTests()
    {
        var container = _mocker.GetMock<Container>();
        _mocker.GetMock<ICosmosDbContainerFactory>()
            .Setup(x => x.CreateEpisodesContainer())
            .Returns(() => container.Object);
        container.Setup(x => x.PatchItemAsync<JsonElement>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<IReadOnlyList<PatchOperation>>(),
                It.IsAny<PatchItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> operations,
                PatchItemRequestOptions options, CancellationToken _) =>
            {
                _patchCalls.Add(new PatchCall(id, partitionKey, operations, options));
                return _patchException is null
                    ? Task.FromResult(new Mock<ItemResponse<JsonElement>>().Object)
                    : Task.FromException<ItemResponse<JsonElement>>(_patchException);
            });
    }

    [Fact(DisplayName =
        "youtube-id-only patch: writes exactly one Set of /ids/youtube with the video id, in the episode's podcast partition, under the plan's own filter predicate.")]
    public async Task patch_sets_only_ids_youtube_under_plan_predicate()
    {
        // Arrange
        var plan = Plan(idsObjectExists: true);
        var sut = _mocker.CreateInstance<CosmosEpisodeYouTubeIdRepairStore>();

        // Act
        var outcome = await sut.Patch(plan);

        // Assert
        outcome.Should().Be(YouTubeIdPatchOutcome.Patched);
        var call = _patchCalls.Should().ContainSingle().Subject;
        call.Id.Should().Be(plan.EpisodeId.ToString());
        call.PartitionKey.Should().Be(new PartitionKey(plan.PodcastId.ToString()));
        var operation = call.Operations.Should().ContainSingle().Subject;
        operation.OperationType.Should().Be(PatchOperationType.Set);
        operation.Path.Should().Be("/ids/youtube");
        ((PatchOperation<object>) operation).Value.Should().Be(plan.YouTubeId);
        call.Options.FilterPredicate.Should().Be(EpisodeYouTubeIdRepair.PatchFilterPredicateFor(plan));
        call.Options.FilterPredicate.Should().Contain("c.services.youtube.url = ");
    }

    [Fact(DisplayName =
        "youtube-id-only patch: a document with no ids object gets one Set of /ids holding only youtube, guarded by 'still no ids object'.")]
    public async Task patch_sets_ids_object_when_absent()
    {
        // Arrange
        var plan = Plan(idsObjectExists: false);
        var sut = _mocker.CreateInstance<CosmosEpisodeYouTubeIdRepairStore>();

        // Act
        await sut.Patch(plan);

        // Assert
        var call = _patchCalls.Should().ContainSingle().Subject;
        var operation = call.Operations.Should().ContainSingle().Subject;
        operation.Path.Should().Be("/ids");
        ((PatchOperation<object>) operation).Value.Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["youtube"] = plan.YouTubeId });
        call.Options.FilterPredicate.Should().StartWith("FROM c WHERE (NOT IS_DEFINED(c.ids) OR IS_NULL(c.ids)) AND ");
    }

    [Theory(DisplayName =
        "youtube-id-only patch: Cosmos status codes map to outcomes; 412 is a changed document, 404 a deleted one, 400 an ids object removed since the scan, anything else a failure.")]
    [InlineData(HttpStatusCode.PreconditionFailed, YouTubeIdPatchOutcome.ConditionNotMet)]
    [InlineData(HttpStatusCode.NotFound, YouTubeIdPatchOutcome.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, YouTubeIdPatchOutcome.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests, YouTubeIdPatchOutcome.Failed)]
    [InlineData(HttpStatusCode.InternalServerError, YouTubeIdPatchOutcome.Failed)]
    public async Task patch_maps_cosmos_status(HttpStatusCode status, YouTubeIdPatchOutcome expected)
    {
        // Arrange
        _patchException = new CosmosException("synthetic", status, 0, string.Empty, 0);
        var sut = _mocker.CreateInstance<CosmosEpisodeYouTubeIdRepairStore>();

        // Act
        var outcome = await sut.Patch(Plan(idsObjectExists: true));

        // Assert
        outcome.Should().Be(expected);
    }

    [Fact(DisplayName =
        "youtube-id-only query: --ids are passed as query parameters, never concatenated into the SQL text.")]
    public void build_query_parameterises_ids()
    {
        // Arrange
        var ids = new[] { _fixture.CreateGuid(), _fixture.CreateGuid() };

        // Act
        var query = CosmosEpisodeYouTubeIdRepairStore.BuildQuery(EpisodeYouTubeIdRepair.CandidateQuery, ids);

        // Assert
        query.QueryText.Should().Be(EpisodeYouTubeIdRepair.CandidateQuery + " AND c.id IN (@id0, @id1)");
        query.QueryText.Should().NotContain(ids[0].ToString());
        query.GetQueryParameters().Should().BeEquivalentTo(new[]
        {
            ("@id0", (object) ids[0].ToString()),
            ("@id1", (object) ids[1].ToString())
        });
    }

    [Fact(DisplayName = "youtube-id-only query: without --ids the base query is used unchanged.")]
    public void build_query_without_ids()
    {
        // Arrange
        Guid[] noIds = [];

        // Act
        var query = CosmosEpisodeYouTubeIdRepairStore.BuildQuery(EpisodeYouTubeIdRepair.CandidateQuery, noIds);

        // Assert
        query.QueryText.Should().Be(EpisodeYouTubeIdRepair.CandidateQuery);
        query.GetQueryParameters().Should().BeEmpty();
    }

    private EpisodeYouTubeIdRepairPlan Plan(bool idsObjectExists)
    {
        var videoId = _fixture.CreateYouTubeId();
        return new EpisodeYouTubeIdRepairPlan(
            _fixture.CreateGuid(), _fixture.CreateGuid(), $"https://youtu.be/{videoId}", videoId, idsObjectExists,
            1790000000, false);
    }

    private sealed record PatchCall(
        string Id,
        PartitionKey PartitionKey,
        IReadOnlyList<PatchOperation> Operations,
        PatchItemRequestOptions Options);
}
