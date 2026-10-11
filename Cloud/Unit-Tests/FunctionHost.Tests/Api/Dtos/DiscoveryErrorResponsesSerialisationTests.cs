using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using Api.Dtos;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class DiscoveryErrorResponsesSerialisationTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "The discovery curation 409 body serialises as camelCase message/documentIds, because CF-API and the website read those names.")]
    public void unavailable_reports_body_is_camel_case()
    {
        // Arrange
        var dto = _fixture.Create<DiscoveryUnavailableReportsResponse>();

        // Act
        var json = JsonDocument.Parse(JsonSerializer.Serialize(dto)).RootElement;

        // Assert
        json.EnumerateObject().Select(p => p.Name).Should().Equal("message", "documentIds");
        json.GetProperty("documentIds").EnumerateArray().Select(x => x.GetGuid()).Should().Equal(dto.DocumentIds);
    }

    [Fact(DisplayName =
        "The supervised discovery curation 400 body serialises as camelCase message/resultIds, because CF-API and the website read those names.")]
    public void unknown_result_ids_body_is_camel_case()
    {
        // Arrange
        var dto = _fixture.Create<DiscoveryUnknownResultIdsResponse>();

        // Act
        var json = JsonDocument.Parse(JsonSerializer.Serialize(dto)).RootElement;

        // Assert
        json.EnumerateObject().Select(p => p.Name).Should().Equal("message", "resultIds");
        json.GetProperty("resultIds").EnumerateArray().Select(x => x.GetGuid()).Should().Equal(dto.ResultIds);
    }

    [Fact(DisplayName =
        "The discovery error body serialises as camelCase message, because CF-API and the website read that name.")]
    public void error_body_is_camel_case()
    {
        // Arrange
        var dto = _fixture.Create<DiscoveryErrorResponse>();

        // Act
        var json = JsonDocument.Parse(JsonSerializer.Serialize(dto)).RootElement;

        // Assert
        json.EnumerateObject().Select(p => p.Name).Should().Equal("message");
        json.GetProperty("message").GetString().Should().Be(dto.Message);
    }
}
