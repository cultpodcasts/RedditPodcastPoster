using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Serialization;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Models.Subjects;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

/// <summary>#1513: <c>source</c> moved from a string to <see cref="SubjectMatchSourceDto"/>; the JSON bytes must not change.</summary>
public class SubjectMatchSourceDtoJsonEquivalenceTests
{
    private static readonly JsonSerializerOptions HttpOptions = ApiJson.CreateHttpSerializerOptions();
    private readonly Fixture _fixture = new();

    [Theory(DisplayName =
        "When a domain SubjectMatchSource is mapped to SubjectMatchSourceDto and written with the match property's string converter, the JSON is byte-identical to the old string property, so clients see the same source.")]
    [InlineData(SubjectMatchSource.Title)]
    [InlineData(SubjectMatchSource.Description)]
    [InlineData(SubjectMatchSource.PodcastDefault)]
    public void source_json_matches_the_old_string(SubjectMatchSource source)
    {
        // Arrange
        var before = new StringSourceProperty { Source = source.ToString() };
        var after = new DtoSourceProperty { Source = source.ToDto() };

        // Act
        var beforeJson = JsonSerializer.Serialize(before, HttpOptions);
        var afterJson = JsonSerializer.Serialize(after, HttpOptions);

        // Assert
        afterJson.Should().Be(beforeJson);
    }

    [Fact(DisplayName =
        "When every domain SubjectMatchSource is mapped, the DTO has the same names and numbers, so the API enum cannot drift from the domain enum.")]
    public void subject_match_source_dto_matches_every_domain_member()
    {
        // Arrange
        var domainValues = Enum.GetValues<SubjectMatchSource>();

        // Act
        var mapped = domainValues.Select(value => (Source: value, Dto: value.ToDto())).ToArray();

        // Assert
        Enum.GetValues<SubjectMatchSourceDto>().Should().HaveSameCount(domainValues);
        mapped.Should().AllSatisfy(pair =>
        {
            pair.Dto.ToString().Should().Be(pair.Source.ToString());
            ((int)pair.Dto).Should().Be((int)pair.Source);
        });
    }

    [Fact(DisplayName =
        "When a SubjectMatchSource value is not a known member, mapping throws, so an unknown source is never sent under a different JSON name.")]
    public void unknown_subject_match_source_throws()
    {
        // Arrange
        var unknown = (SubjectMatchSource)int.MaxValue;

        // Act
        var act = () => unknown.ToDto();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName =
        "When a subject match DTO is written with PodcastDefault, the JSON carries the member name as before, so episode matches and discovery subjectMatches are unchanged.")]
    public void subject_match_dto_writes_the_source_name()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var term = _fixture.Create<string>();
        var dto = new SubjectMatchDto
        {
            Subject = subject,
            Term = term,
            Source = SubjectMatchSourceDto.PodcastDefault
        };

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, HttpOptions));

        // Assert
        json.RootElement.GetProperty("source").GetString().Should().Be(nameof(SubjectMatchSource.PodcastDefault));
        json.RootElement.GetProperty("subject").GetString().Should().Be(subject);
        json.RootElement.GetProperty("term").GetString().Should().Be(term);
    }

    /// <summary>The match JSON property as it was when source was a string.</summary>
    private sealed class StringSourceProperty
    {
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>The same property after the boundary enum.</summary>
    private sealed class DtoSourceProperty
    {
        [JsonPropertyName("source")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SubjectMatchSourceDto Source { get; set; }
    }
}
