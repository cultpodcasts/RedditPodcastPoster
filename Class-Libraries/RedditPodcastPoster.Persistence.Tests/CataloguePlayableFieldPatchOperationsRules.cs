using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class CataloguePlayableFieldPatchOperationsRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly CosmosClient _client;
    private readonly List<(IReadOnlyList<PatchOperation> Operations, PatchItemRequestOptions Options)> _sent = [];

    public CataloguePlayableFieldPatchOperationsRules()
    {
        _client = new CosmosClient(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
            });
        _mocker.GetMock<Database>().Setup(x => x.Client).Returns(() => _client);
        _mocker.GetMock<Container>().Setup(x => x.Database).Returns(() => _mocker.Get<Database>());
        _mocker.GetMock<Container>()
            .Setup(x => x.PatchItemAsync<object>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<IReadOnlyList<PatchOperation>>(),
                It.IsAny<PatchItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, PartitionKey _, IReadOnlyList<PatchOperation> operations, PatchItemRequestOptions options,
                CancellationToken _) =>
            {
                _sent.Add((operations, options));
                return Task.FromResult<ItemResponse<object>>(null!);
            });
    }

    public void Dispose() => _client.Dispose();

    [Fact(DisplayName =
        "Playable field patch: clearing services or youTubeId removes those paths when the read showed the member " +
        "defined, because an explicit JSON null is still a stored member and a full save omits nulls (#1154).")]
    public void defined_null_members_are_removed()
    {
        // Arrange
        var patch = ClearBoth(servicesDefined: true, youTubeIdDefined: true);

        // Act
        var operations = CataloguePlayableFieldPatchOperations.Build(patch);

        // Assert
        operations.Should().HaveCount(2);
        operations.Should().OnlyContain(x => x.OperationType == PatchOperationType.Remove);
        operations.Select(x => x.Path).Should().BeEquivalentTo("/services", "/youTubeId");
    }

    [Fact(DisplayName =
        "Playable field patch: clearing services or youTubeId emits no operation when the read showed the member " +
        "already absent, because Cosmos returns 400 when Remove targets a path that is not defined (#1154).")]
    public void absent_null_members_emit_no_operation()
    {
        // Arrange
        var patch = ClearBoth(servicesDefined: false, youTubeIdDefined: false);

        // Act
        var operations = CataloguePlayableFieldPatchOperations.Build(patch);

        // Assert
        operations.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Playable field patch: a null desired value is never Set, and only the member the read showed as defined " +
        "is removed, because a full save omits nulls and an undefined Remove is a Cosmos 400 (#1154).")]
    public void null_desired_value_is_never_set()
    {
        // Arrange
        var patch = ClearBoth(servicesDefined: true, youTubeIdDefined: false);

        // Act
        var operations = CataloguePlayableFieldPatchOperations.Build(patch);

        // Assert
        operations.Should().ContainSingle();
        operations.Single().OperationType.Should().Be(PatchOperationType.Remove);
        operations.Single().Path.Should().Be("/services");
    }

    [Fact(DisplayName =
        "Playable field patch: a services map and a youTube id are set on those paths only, because the patch " +
        "must not touch any other playable field.")]
    public void present_fields_are_set()
    {
        // Arrange
        var services = new Dictionary<string, ServiceLink>
        {
            ["youtube"] = new ServiceLink { Url = new Uri("https://www.youtube.com/watch?v=abcdefghijk") }
        };
        var patch = new CataloguePlayableFieldPatch
        {
            SetServices = true,
            Services = services,
            SetYouTubeId = true,
            YouTubeId = "abcdefghijk"
        };

        // Act
        var operations = CataloguePlayableFieldPatchOperations.Build(patch);

        // Assert
        operations.Should().HaveCount(2);
        operations.Should().OnlyContain(x => x.OperationType == PatchOperationType.Set);
        operations.Select(x => x.Path).Should().BeEquivalentTo("/services", "/youTubeId");
    }

    [Fact(DisplayName =
        "Playable field patch: filling youTubeId when the member was absent uses Set (not Replace) on /youTubeId, " +
        "because Replace fails when the property is missing and playables written before compact youTubeId still " +
        "need the id added.")]
    public void absent_youtube_id_fill_uses_set_not_replace()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var patch = new CataloguePlayableFieldPatch
        {
            SetYouTubeId = true,
            YouTubeId = videoId,
            YouTubeIdDefined = false
        };

        // Act
        var operations = CataloguePlayableFieldPatchOperations.Build(patch);

        // Assert
        var youTubeOp = operations.Should().ContainSingle().Subject;
        youTubeOp.Path.Should().Be("/youTubeId");
        youTubeOp.OperationType.Should().Be(PatchOperationType.Set);
        youTubeOp.OperationType.Should().NotBe(PatchOperationType.Replace);
    }

    [Fact(DisplayName =
        "Playable field patch: the ETag read records services and youTubeId as defined when the raw document " +
        "contains them, including an explicit JSON null, and as absent when they are omitted, because a typed " +
        "model cannot tell those shapes apart (#1154).")]
    public async Task read_records_member_presence_including_stored_null()
    {
        // Arrange
        var definedId = _fixture.CreateGuid();
        var omittedId = _fixture.CreateGuid();
        var tvShowId = _fixture.CreateGuid();
        var title = _fixture.CreateTitle();
        var definedETag = _fixture.Create<string>();
        var omittedETag = _fixture.Create<string>();
        var defined = Document(definedId, title, includeNullMembers: true);
        var omitted = Document(omittedId, title, includeNullMembers: false);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<JsonObject>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, PartitionKey _, ItemRequestOptions _, CancellationToken _) =>
            {
                var raw = id == definedId.ToString() ? defined : omitted;
                var etag = id == definedId.ToString() ? definedETag : omittedETag;
                return Mock.Of<ItemResponse<JsonObject>>(r => r.Resource == raw && r.ETag == etag);
            });
        var sut = _mocker.CreateInstance<TvShowEpisodeRepository>();

        // Act
        var storedNull = await sut.GetTvShowEpisodeWithETag(tvShowId, definedId);
        var missing = await sut.GetTvShowEpisodeWithETag(tvShowId, omittedId);

        // Assert
        storedNull.Should().NotBeNull();
        storedNull!.ETag.Should().Be(definedETag);
        storedNull.ServicesDefined.Should().BeTrue();
        storedNull.YouTubeIdDefined.Should().BeTrue();
        storedNull.Item.Services.Should().BeNull();
        storedNull.Item.YouTubeId.Should().BeNull();
        missing.Should().NotBeNull();
        missing!.ETag.Should().Be(omittedETag);
        missing.ServicesDefined.Should().BeFalse();
        missing.YouTubeIdDefined.Should().BeFalse();
        missing.Item.Services.Should().BeNull();
        missing.Item.YouTubeId.Should().BeNull();
    }

    [Fact(DisplayName =
        "Playable field patch: clearing a member that is already absent sends no request, and clearing a defined " +
        "member is one IfMatch removal with no IS_DEFINED filter, because a 412 on this patch is an ETag conflict " +
        "and an empty patch is a Cosmos 400 (#1154).")]
    public async Task apply_removes_only_defined_members_in_one_if_match_patch()
    {
        // Arrange
        var tvShowId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var etag = _fixture.Create<string>();
        var sut = _mocker.CreateInstance<TvShowEpisodeRepository>();

        // Act
        var absent = await sut.PatchFields(tvShowId, episodeId, etag, ClearBoth(false, false));
        var defined = await sut.PatchFields(tvShowId, episodeId, etag, ClearBoth(true, true));

        // Assert
        absent.Should().Be(CataloguePlayablePatchOutcome.Patched);
        defined.Should().Be(CataloguePlayablePatchOutcome.Patched);
        _sent.Should().ContainSingle();
        _sent[0].Options.IfMatchEtag.Should().Be(etag);
        _sent[0].Options.FilterPredicate.Should().BeNull();
        _sent[0].Operations.Should().OnlyContain(x => x.OperationType == PatchOperationType.Remove);
        _sent[0].Operations.Select(x => x.Path).Should().BeEquivalentTo("/services", "/youTubeId");
    }

    private static CataloguePlayableFieldPatch ClearBoth(bool servicesDefined, bool youTubeIdDefined) => new()
    {
        SetServices = true,
        Services = null,
        ServicesDefined = servicesDefined,
        SetYouTubeId = true,
        YouTubeId = null,
        YouTubeIdDefined = youTubeIdDefined
    };

    private static JsonObject Document(Guid id, string title, bool includeNullMembers)
    {
        var json = includeNullMembers
            ? JsonSerializer.Serialize(new { id, title, services = (object?)null, youTubeId = (string?)null })
            : JsonSerializer.Serialize(new { id, title });
        return JsonNode.Parse(json)!.AsObject();
    }
}
