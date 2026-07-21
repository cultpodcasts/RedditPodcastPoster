using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Models;

namespace RedditPodcastPoster.People.Tests;

public class EpisodeGuestEnricherTests
{
    private readonly Mock<IPersonService> _personService = new();

    private EpisodeGuestEnricher CreateSut() =>
        new(_personService.Object, Mock.Of<ILogger<EpisodeGuestEnricher>>());

    [Fact]
    public async Task EnrichGuests_HighConfidenceTitleMatch_UnionsCanonicalName()
    {
        var episode = CreateEpisode("Interview with Ada Example");
        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Ada Example", null, null),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        result.SkippedLowConfidence.Should().BeEmpty();
        episode.Guests.Should().Equal("Ada Example");
    }

    [Fact]
    public async Task EnrichGuests_ShortTerm_SkippedAsLowConfidence()
    {
        var episode = CreateEpisode("Chat with Sam about widgets");
        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Sam", null, null),
            [new PersonMatchResult("Sam", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().BeEmpty();
        result.SkippedLowConfidence.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Sam");
        episode.Guests.Should().BeNull();
    }

    [Fact]
    public async Task EnrichGuests_NeverRemovesExistingGuests()
    {
        var episode = CreateEpisode("Interview with Ada Example");
        episode.Guests = ["Existing Guest"];

        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Ada Example", null, null),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        episode.Guests.Should().BeEquivalentTo(["Existing Guest", "Ada Example"]);
    }

    [Fact]
    public async Task EnrichGuests_DoesNotDuplicateExistingGuest_CaseInsensitive()
    {
        var episode = CreateEpisode("Interview with Ada Example");
        episode.Guests = ["ada example"];

        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Ada Example", null, null),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().BeEmpty();
        episode.Guests.Should().Equal("ada example");
    }

    [Fact]
    public async Task EnrichGuests_MinMatchCount_SkipsBelowThreshold()
    {
        var episode = CreateEpisode("Interview with Ada Example");
        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Ada Example", null, null),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(
            episode,
            GuestEnrichmentOptions.Default with { MinMatchCount = 2 });

        result.Additions.Should().BeEmpty();
        result.SkippedLowConfidence.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        episode.Guests.Should().BeNull();
    }

    [Fact]
    public async Task EnrichGuests_MinMatchCount_AcceptsAtOrAboveThreshold()
    {
        var episode = CreateEpisode("Interview with Ada Example");
        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Ada Example", null, null),
            [new PersonMatchResult("Ada Example", 2)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(
            episode,
            GuestEnrichmentOptions.Default with { MinMatchCount = 2 });

        result.Additions.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        episode.Guests.Should().Equal("Ada Example");
    }

    [Fact]
    public async Task EnrichGuests_WithDescription_PassesWithDescriptionTrue()
    {
        var episode = CreateEpisode("Some title");
        _personService
            .Setup(x => x.MatchEpisode(episode, true))
            .ReturnsAsync([]);

        await CreateSut().EnrichGuests(
            episode,
            GuestEnrichmentOptions.Default with { TitleOnly = false });

        _personService.Verify(x => x.MatchEpisode(episode, true), Times.Once);
        _personService.Verify(x => x.MatchEpisode(episode, false), Times.Never);
    }

    private static Episode CreateEpisode(string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Description mentioning someone else",
            Release = DateTime.UtcNow,
            Length = TimeSpan.FromMinutes(30),
            Explicit = false
        };
}
