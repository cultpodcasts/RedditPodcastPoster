using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using FluentAssertions;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastKindTransferRequestJsonRules
{
    private readonly DomainTestFixture _fixture = new();

    private static readonly JsonObjectSerializer IsolatedSerializer = new(new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    [Fact(DisplayName =
        "Podcast kind transfer JSON uses the CatalogueParentKind names TvShow and NewsOrganisation, not numbers.")]
    public void target_kind_round_trips_as_enum_names()
    {
        // Arrange
        var json = "{\"targetKind\":\"NewsOrganisation\"}";

        // Act
        var request = JsonSerializer.Deserialize<global::Api.Dtos.PodcastKindTransferRequest>(json);
        var responseJson = JsonSerializer.Serialize(new PodcastKindTransferResponse
        {
            TargetKind = CatalogueParentKind.TvShow,
            PlayableCount = 1
        });

        // Assert
        request.Should().NotBeNull();
        request!.TargetKind.Should().Be(CatalogueParentKind.NewsOrganisation);
        responseJson.Should().Contain("\"targetKind\":\"TvShow\"");
        responseJson.Should().NotContain("\"targetKind\":0");
    }

    [Fact(DisplayName =
        "Deserializing targetKind Film throws, because Film is not a CatalogueParentKind.")]
    public void film_target_kind_does_not_deserialize()
    {
        // Arrange
        var json = "{\"targetKind\":\"Film\"}";

        // Act
        var act = () => JsonSerializer.Deserialize<global::Api.Dtos.PodcastKindTransferRequest>(json);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName =
        "Isolated [FromBody] PodcastKindTransferRequest: integer targetKind 0 throws JsonException, because CatalogueParentKind is string names only.")]
    public async Task integer_target_kind_throws_json_exception()
    {
        // Arrange
        const string json = "{\"targetKind\":0}";

        // Act
        var act = () => BindAsync(json);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact(DisplayName =
        "PodcastKindTransferResult.ToDto copies parent id, target kind, playable count, and indexing failure onto the HTTP response.")]
    public void result_to_dto_maps_accepted_fields()
    {
        // Arrange
        var parentId = _fixture.CreateGuid();
        var result = new PodcastKindTransferResult(
            PodcastKindTransferStatus.Accepted,
            parentId,
            CatalogueParentKind.NewsOrganisation,
            PlayableCount: 3,
            FailureIndexingPlayables: true);

        // Act
        var dto = result.ToDto();

        // Assert
        dto.ParentId.Should().Be(parentId);
        dto.TargetKind.Should().Be(CatalogueParentKind.NewsOrganisation);
        dto.PlayableCount.Should().Be(3);
        dto.FailureIndexingPlayables.Should().BeTrue();
    }

    private static async Task<global::Api.Dtos.PodcastKindTransferRequest> BindAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var bound = await IsolatedSerializer.DeserializeAsync(
            stream, typeof(global::Api.Dtos.PodcastKindTransferRequest), CancellationToken.None);
        return (global::Api.Dtos.PodcastKindTransferRequest)bound!;
    }
}
