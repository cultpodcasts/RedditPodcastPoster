using System.Text.Json;
using Api.Dtos;
using Api.Dtos.Extensions;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Subjects;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class DiscoveryResponseSubjectMatchesTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "When the discovery API maps a result, it returns subjectMatches with the matched term and a string source, so the website can show why each subject matched.")]
    public void discovery_response_item_exposes_subject_matches()
    {
        // Arrange
        var match = new PlayableSubjectMatch
        {
            Subject = _fixture.Create<string>(), Term = _fixture.Create<string>(),
            Source = SubjectMatchSource.Title
        };
        var item = new DiscoveryResult { Subjects = [match.Subject], SubjectMatches = [match] };

        // Act
        var dto = item.ToDiscoveryResponseItem(new Dictionary<Guid, DiscoveryResponse.Item.MatchingPodcast>());
        var json = JsonSerializer.Serialize(dto);

        // Assert
        dto.SubjectMatches.Should().ContainSingle(m => m.Term == match.Term && m.Subject == match.Subject);
        json.Should().Contain($"\"subjectMatches\":[{{\"subject\":\"{match.Subject}\",\"term\":\"{match.Term}\",\"source\":\"Title\"}}]");
    }

    [Theory(DisplayName =
        "When the API serialises a result, historic (null) subjectMatches is written as null and recorded-but-empty as [], so the website can tell 'not recorded' from 'no evidence'.")]
    [InlineData(true, "\"subjectMatches\":null")]
    [InlineData(false, "\"subjectMatches\":[]")]
    public void discovery_response_item_preserves_null_versus_empty(bool historic, string expectedJson)
    {
        // Arrange
        var item = new DiscoveryResult
        {
            Subjects = [_fixture.Create<string>()],
            SubjectMatches = historic ? null : []
        };

        // Act
        var dto = item.ToDiscoveryResponseItem(new Dictionary<Guid, DiscoveryResponse.Item.MatchingPodcast>());
        // Functions worker default serializer: web defaults, nulls are not ignored.
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Assert
        json.Should().Contain(expectedJson);
    }
}
