using Api.Dtos;
using Api.Dtos.Mapping;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.People.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class PlayableGuestDtoEnricherRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private sealed class ProbeDto : IPlayableGuestFieldsDto
    {
        public string[]? Guests { get; set; }
        public IList<PersonDto>? GuestPeople { get; set; }
        public IList<EpisodeDto.PersonMatch>? GuestSuggestions { get; set; }
    }

    [Fact(DisplayName =
        "Playable GET guest enrichment: a person named only in the description is returned as a guestSuggestion " +
        "and not as guestPeople when they are not already stored as a guest.")]
    public async Task description_only_person_is_suggested()
    {
        // Arrange
        var guestName = "Synthetic Description Guest";
        var person = new Person(guestName) { Id = _fixture.CreateGuid() };
        var match = new PersonMatch(person, [new PersonMatchResult(guestName, 2)]);
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.MatchEpisode(It.IsAny<RedditPodcastPoster.Models.Episodes.Episode>(), true))
            .ReturnsAsync([match]);
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.GetByNames(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);
        var sut = _mocker.CreateInstance<PlayableGuestDtoEnricher>();
        var dto = new ProbeDto();

        // Act
        await sut.EnrichAsync(
            dto,
            title: "Weekly briefing without the guest name",
            description: $"W5 speaks with {guestName} about the case.",
            language: null,
            guests: null);

        // Assert
        dto.GuestPeople.Should().BeNullOrEmpty();
        dto.GuestSuggestions.Should().ContainSingle()
            .Which.Person.Name.Should().Be(guestName);
    }

    [Fact(DisplayName =
        "Playable GET guest enrichment: a stored guest is resolved into guestPeople and omitted from guestSuggestions.")]
    public async Task stored_guest_is_guest_people_not_suggestion()
    {
        // Arrange
        var guestName = "Synthetic Stored Guest";
        var person = new Person(guestName) { Id = _fixture.CreateGuid() };
        var match = new PersonMatch(person, [new PersonMatchResult(guestName, 2)]);
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.GetByNames(It.Is<IEnumerable<string>>(n => n.Contains(guestName))))
            .ReturnsAsync([person]);
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.MatchEpisode(It.IsAny<RedditPodcastPoster.Models.Episodes.Episode>(), true))
            .ReturnsAsync([match]);
        var sut = _mocker.CreateInstance<PlayableGuestDtoEnricher>();
        var dto = new ProbeDto { Guests = [guestName] };

        // Act
        await sut.EnrichAsync(
            dto,
            title: guestName,
            description: $"About {guestName}.",
            language: null,
            guests: [guestName]);

        // Assert
        dto.GuestPeople.Should().ContainSingle().Which.Name.Should().Be(guestName);
        dto.GuestSuggestions.Should().BeEmpty();
    }
}
