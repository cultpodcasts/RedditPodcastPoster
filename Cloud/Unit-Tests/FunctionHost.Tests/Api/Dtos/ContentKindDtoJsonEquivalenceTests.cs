using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Serialization;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Models.ContentKinds;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

/// <summary>#1508: <c>contentKind</c> moved from a string to <see cref="ContentKindDto"/>; the JSON bytes must not change.</summary>
public class ContentKindDtoJsonEquivalenceTests
{
    private static readonly JsonSerializerOptions HttpOptions = ApiJson.CreateHttpSerializerOptions();
    private readonly Fixture _fixture = new();

    [Theory(DisplayName =
        "When a stored kind name is mapped to ContentKindDto and written with the episode property's string converter, the JSON is byte-identical to the old string property, so clients see the same contentKind.")]
    [InlineData(nameof(ContentKind.Episode))]
    [InlineData(nameof(ContentKind.TvShowEpisode))]
    [InlineData(nameof(ContentKind.Film))]
    [InlineData(nameof(ContentKind.NewsReport))]
    public void content_kind_json_matches_the_old_string(string kind)
    {
        // Arrange
        var before = new StringKindProperty { ContentKind = kind };
        var after = new DtoKindProperty { ContentKind = kind.ToContentKindDto() };

        // Act
        var beforeJson = JsonSerializer.Serialize(before, HttpOptions);
        var afterJson = JsonSerializer.Serialize(after, HttpOptions);

        // Assert
        afterJson.Should().Be(beforeJson);
    }

    [Fact(DisplayName =
        "When there is no kind, both the old string property and the ContentKindDto property omit contentKind, because WhenWritingNull is unchanged.")]
    public void null_content_kind_omits_the_same_key()
    {
        // Arrange
        var before = new StringKindProperty();
        var after = new DtoKindProperty { ContentKind = ((string?)null).ToContentKindDto() };

        // Act
        var beforeJson = JsonSerializer.Serialize(before, HttpOptions);
        var afterJson = JsonSerializer.Serialize(after, HttpOptions);

        // Assert
        after.ContentKind.Should().BeNull();
        afterJson.Should().Be(beforeJson);
        afterJson.Should().Be("{}");
    }

    [Fact(DisplayName =
        "When every domain ContentKind is mapped, the DTO has the same names and numbers, so the API enum cannot drift from the domain enum.")]
    public void content_kind_dto_matches_every_domain_member()
    {
        // Arrange
        var domainValues = Enum.GetValues<ContentKind>();

        // Act
        var mapped = domainValues.Select(value => (Source: value, Dto: value.ToDto(), FromName: value.ToString().ToContentKindDto())).ToArray();

        // Assert
        Enum.GetValues<ContentKindDto>().Should().HaveSameCount(domainValues);
        mapped.Should().AllSatisfy(pair =>
        {
            pair.Dto.ToString().Should().Be(pair.Source.ToString());
            ((int)pair.Dto).Should().Be((int)pair.Source);
            pair.FromName.Should().Be(pair.Dto);
        });
    }

    [Fact(DisplayName =
        "When a stored kind name is not a ContentKind member, mapping throws, so an unknown kind is never sent under a different JSON name.")]
    public void unknown_content_kind_name_throws()
    {
        // Arrange
        var unknown = _fixture.Create<string>();

        // Act
        var act = () => unknown.ToContentKindDto();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName =
        "When a news-report kind is applied to the public and curator episode DTOs, both write \"contentKind\":\"NewsReport\" as before, so the GET-by-id responses are unchanged.")]
    public void episode_dtos_write_the_kind_name()
    {
        // Arrange
        var kind = new PlayableKind(nameof(ContentKind.NewsReport), _fixture.Create<Guid>(), _fixture.Create<string>(),
            _fixture.Create<string>(), _fixture.Create<string>());

        // Act
        var publicJson = JsonDocument.Parse(JsonSerializer.Serialize(new PublicEpisodeDto().WithKind(kind), HttpOptions)).RootElement;
        var curatorJson = JsonDocument.Parse(JsonSerializer.Serialize(new EpisodeDto().WithKind(kind), HttpOptions)).RootElement;

        // Assert
        publicJson.GetProperty("contentKind").GetString().Should().Be(nameof(ContentKind.NewsReport));
        curatorJson.GetProperty("contentKind").GetString().Should().Be(nameof(ContentKind.NewsReport));
    }

    /// <summary>The episode JSON property as it was when contentKind was a string.</summary>
    private sealed class StringKindProperty
    {
        [JsonPropertyName("contentKind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContentKind { get; set; }
    }

    /// <summary>The same property after the boundary enum.</summary>
    private sealed class DtoKindProperty
    {
        [JsonPropertyName("contentKind")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ContentKindDto? ContentKind { get; set; }
    }
}
