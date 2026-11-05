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
using Episode = RedditPodcastPoster.Models.Episodes.Episode;

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

    public PlayableGuestDtoEnricherRules()
    {
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.GetByNames(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);
    }

    [Fact(DisplayName =
        "Playable GET guest enrichment: MatchEpisode is invoked with the EnrichAsync title and description " +
        "and withDescription true; a person returned only by that match becomes a guestSuggestion and not guestPeople " +
        "when they are not already stored as a guest.")]
    public async Task description_only_person_is_suggested()
    {
        // Arrange
        var guestName = "Synthetic Description Guest";
        var title = "Weekly briefing without the guest name";
        var description = $"W5 speaks with {guestName} about the case.";
        var person = new Person(guestName) { Id = _fixture.CreateGuid() };
        var match = new PersonMatch(person, [new PersonMatchResult(guestName, 2)]);
        Episode? capturedProbe = null;
        bool? capturedWithDescription = null;
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.MatchEpisode(It.IsAny<Episode>(), It.IsAny<bool>()))
            .Callback<Episode, bool>((probe, withDescription) =>
            {
                capturedProbe = probe;
                capturedWithDescription = withDescription;
            })
            .ReturnsAsync([match]);
        var sut = _mocker.CreateInstance<PlayableGuestDtoEnricher>();
        var dto = new ProbeDto();

        // Act
        await sut.EnrichAsync(
            dto,
            title: title,
            description: description,
            language: null,
            guests: null);

        // Assert
        capturedProbe.Should().NotBeNull();
        capturedProbe!.Title.Should().Be(title);
        capturedProbe.Description.Should().Be(description);
        capturedWithDescription.Should().BeTrue();
        dto.GuestPeople.Should().BeNullOrEmpty();
        dto.GuestSuggestions.Should().ContainSingle()
            .Which.Person.Name.Should().Be(guestName);
    }

    [Fact(DisplayName =
        "Playable GET guest enrichment: MatchEpisode is invoked with the EnrichAsync title and description " +
        "and withDescription true; a stored guest is resolved into guestPeople and omitted from guestSuggestions.")]
    public async Task stored_guest_is_guest_people_not_suggestion()
    {
        // Arrange
        var guestName = "Synthetic Stored Guest";
        var title = guestName;
        var description = $"About {guestName}.";
        var person = new Person(guestName) { Id = _fixture.CreateGuid() };
        var match = new PersonMatch(person, [new PersonMatchResult(guestName, 2)]);
        Episode? capturedProbe = null;
        bool? capturedWithDescription = null;
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.GetByNames(It.Is<IEnumerable<string>>(n => n.Contains(guestName))))
            .ReturnsAsync([person]);
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.MatchEpisode(It.IsAny<Episode>(), It.IsAny<bool>()))
            .Callback<Episode, bool>((probe, withDescription) =>
            {
                capturedProbe = probe;
                capturedWithDescription = withDescription;
            })
            .ReturnsAsync([match]);
        var sut = _mocker.CreateInstance<PlayableGuestDtoEnricher>();
        var dto = new ProbeDto { Guests = [guestName] };

        // Act
        await sut.EnrichAsync(
            dto,
            title: title,
            description: description,
            language: null,
            guests: [guestName]);

        // Assert
        capturedProbe.Should().NotBeNull();
        capturedProbe!.Title.Should().Be(title);
        capturedProbe.Description.Should().Be(description);
        capturedWithDescription.Should().BeTrue();
        dto.GuestPeople.Should().ContainSingle().Which.Name.Should().Be(guestName);
        dto.GuestSuggestions.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Playable GET guest enrichment: when includeGuestSuggestions is false, MatchEpisode is not called " +
        "and GuestSuggestions stay unset.")]
    public async Task include_guest_suggestions_false_skips_match()
    {
        // Arrange
        var guestName = "Synthetic Stored Guest";
        var person = new Person(guestName) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.GetByNames(It.Is<IEnumerable<string>>(n => n.Contains(guestName))))
            .ReturnsAsync([person]);
        var sut = _mocker.CreateInstance<PlayableGuestDtoEnricher>();
        var dto = new ProbeDto { Guests = [guestName] };

        // Act
        await sut.EnrichAsync(
            dto,
            title: guestName,
            description: $"About {guestName}.",
            language: null,
            guests: [guestName],
            includeGuestSuggestions: false);

        // Assert
        dto.GuestPeople.Should().ContainSingle().Which.Name.Should().Be(guestName);
        dto.GuestSuggestions.Should().BeNull();
        _mocker.GetMock<IPersonService>()
            .Verify(x => x.MatchEpisode(It.IsAny<Episode>(), It.IsAny<bool>()), Times.Never);
    }
}
