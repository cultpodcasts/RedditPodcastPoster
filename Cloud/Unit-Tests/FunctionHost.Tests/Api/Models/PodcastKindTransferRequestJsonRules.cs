using System.Text.Json;
using FluentAssertions;
using Api.Dtos;
using Api.Models;
using RedditPodcastPoster.Models.Catalogue;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class PodcastKindTransferRequestJsonRules
{
    [Fact(DisplayName =
        "Podcast kind transfer JSON uses the CatalogueParentKind names TvShow and NewsOrganisation, not numbers.")]
    public void target_kind_round_trips_as_enum_names()
    {
        // Arrange
        var json = "{\"targetKind\":\"NewsOrganisation\"}";

        // Act
        var request = JsonSerializer.Deserialize<PodcastKindTransferRequest>(json);
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
        var act = () => JsonSerializer.Deserialize<PodcastKindTransferRequest>(json);

        // Assert
        act.Should().Throw<JsonException>();
    }
}
