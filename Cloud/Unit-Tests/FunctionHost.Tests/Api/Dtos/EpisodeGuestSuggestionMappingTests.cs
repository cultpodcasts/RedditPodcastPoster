using FluentAssertions;
using Api.Dtos.Mapping;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class EpisodeGuestSuggestionMappingTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Plain English rule: when a guest suggestion is mapped, then the person includes sort name, organization, and aliases, because suggestions use the same person fields as a person read.")]
    public void guest_suggestion_copies_sort_name_organization_and_aliases()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var name = _fixture.CreateTitle();
        var sortName = _fixture.Create<string>();
        var twitter = _fixture.Create<string>();
        var bluesky = _fixture.Create<string>();
        var alias = _fixture.Create<string>();
        var term = _fixture.Create<string>();
        var person = new Person(name)
        {
            Id = id,
            SortName = sortName,
            TwitterHandle = twitter,
            BlueskyHandle = bluesky,
            IsOrganization = true,
            Aliases = [alias]
        };
        var match = new PersonMatch(person, [new PersonMatchResult(term, 1)]);

        // Act
        var dto = EpisodeDtoMapper.ToPersonMatch(match);

        // Assert
        dto.Person.Id.Should().Be(id);
        dto.Person.Name.Should().Be(name);
        dto.Person.SortName.Should().Be(sortName);
        dto.Person.IsOrganization.Should().BeTrue();
        dto.Person.Aliases.Should().Equal(alias);
        dto.Person.TwitterHandle.Should().Be(twitter);
        dto.Person.BlueskyHandle.Should().Be(bluesky);
        dto.MatchResults.Should().ContainSingle().Which.Term.Should().Be(term);
    }
}
