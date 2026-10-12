using AutoFixture;
using FluentAssertions;
using Api.Handlers;
using Xunit;

namespace FunctionHost.Tests.Api.Handlers;

public class ClaimValueParserTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "A supervisors claim given as repeated claims or as one JSON-array string yields the same distinct subjects, because the auth front-end may deliver either shape.")]
    public void repeated_and_json_array_claims_are_equivalent()
    {
        // Arrange
        var first = _fixture.Create<string>();
        var second = _fixture.Create<string>();
        var json = System.Text.Json.JsonSerializer.Serialize(new[] { first, second, first });

        // Act
        var fromRepeated = ClaimValueParser.Parse([first, second, " ", null]);
        var fromJson = ClaimValueParser.Parse([json]);

        // Assert
        fromRepeated.Should().Equal(first, second);
        fromJson.Should().Equal(first, second);
    }
}
