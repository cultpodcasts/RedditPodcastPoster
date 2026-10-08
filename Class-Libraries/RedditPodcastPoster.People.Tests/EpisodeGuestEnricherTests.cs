using Microsoft.Extensions.Logging;
using FluentAssertions;
using Moq;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.People.Services;

namespace RedditPodcastPoster.People.Tests;

public class EpisodeGuestEnricherTests
{
    private readonly Mock<IPersonService> _personService = new();

    private EpisodeGuestEnricher CreateSut() =>
        new(_personService.Object, Mock.Of<ILogger<EpisodeGuestEnricher>>());

    [Fact(DisplayName =
        "When a title match is high confidence, then the canonical name is added to guests, because enrichment unions the matched person.")]
    public async Task EnrichGuests_HighConfidenceTitleMatch_UnionsCanonicalName()
    {
        // Arrange
        var episode = CreateEpisode("Interview with Ada Example");
        var match = new PersonMatch(
            new Person("Ada Example"),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        // Act
        var result = await CreateSut().EnrichGuests(episode);

        // Assert
        result.Additions.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        result.SkippedLowConfidence.Should().BeEmpty();
        episode.Guests.Should().Equal("Ada Example");
    }

    [Fact(DisplayName =
        "When the only match term is too short, then the guest is skipped, because short terms are low confidence.")]
    public async Task EnrichGuests_ShortTerm_SkippedAsLowConfidence()
    {
        // Arrange
        var episode = CreateEpisode("Chat with Sam about widgets");
        var match = new PersonMatch(
            new Person("Sam"),
            [new PersonMatchResult("Sam", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        // Act
        var result = await CreateSut().EnrichGuests(episode);

        // Assert
        result.Additions.Should().BeEmpty();
        result.SkippedLowConfidence.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Sam");
        episode.Guests.Should().BeNull();
    }

    [Fact(DisplayName =
        "When guests already exist, then enrichment adds the match and keeps the existing names, because enrichment never removes guests.")]
    public async Task EnrichGuests_NeverRemovesExistingGuests()
    {
        // Arrange
        var episode = CreateEpisode("Interview with Ada Example");
        episode.Guests = ["Existing Guest"];

        var match = new PersonMatch(
            new Person("Ada Example"),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        // Act
        var result = await CreateSut().EnrichGuests(episode);

        // Assert
        result.Additions.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        episode.Guests.Should().BeEquivalentTo(["Existing Guest", "Ada Example"]);
    }

    [Fact(DisplayName =
        "When the guest is already on the episode with different casing, then enrichment does not add a duplicate, because matching is case-insensitive.")]
    public async Task EnrichGuests_DoesNotDuplicateExistingGuest_CaseInsensitive()
    {
        // Arrange
        var episode = CreateEpisode("Interview with Ada Example");
        episode.Guests = ["ada example"];

        var match = new PersonMatch(
            new Person("Ada Example"),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        // Act
        var result = await CreateSut().EnrichGuests(episode);

        // Assert
        result.Additions.Should().BeEmpty();
        episode.Guests.Should().Equal("ada example");
    }

    [Fact(DisplayName =
        "When the match count is below the minimum, then the guest is skipped, because the threshold rejects a weak match.")]
    public async Task EnrichGuests_MinMatchCount_SkipsBelowThreshold()
    {
        // Arrange
        var episode = CreateEpisode("Interview with Ada Example");
        var match = new PersonMatch(
            new Person("Ada Example"),
            [new PersonMatchResult("Ada Example", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        // Act
        var result = await CreateSut().EnrichGuests(
            episode,
            GuestEnrichmentOptions.Default with { MinMatchCount = 2 });

        // Assert
        result.Additions.Should().BeEmpty();
        result.SkippedLowConfidence.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        episode.Guests.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the match count meets the minimum, then the guest is added, because the threshold accepts that match.")]
    public async Task EnrichGuests_MinMatchCount_AcceptsAtOrAboveThreshold()
    {
        // Arrange
        var episode = CreateEpisode("Interview with Ada Example");
        var match = new PersonMatch(
            new Person("Ada Example"),
            [new PersonMatchResult("Ada Example", 2)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        // Act
        var result = await CreateSut().EnrichGuests(
            episode,
            GuestEnrichmentOptions.Default with { MinMatchCount = 2 });

        // Assert
        result.Additions.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        episode.Guests.Should().Equal("Ada Example");
    }

    [Fact(DisplayName =
        "When title-only is off, then matching includes the description, because enrichment asks for description matches.")]
    public async Task EnrichGuests_WithDescription_PassesWithDescriptionTrue()
    {
        // Arrange
        var episode = CreateEpisode("Some title");
        _personService
            .Setup(x => x.MatchEpisode(episode, true))
            .ReturnsAsync([]);

        // Act
        await CreateSut().EnrichGuests(
            episode,
            GuestEnrichmentOptions.Default with { TitleOnly = false });

        // Assert
        _personService.Verify(x => x.MatchEpisode(episode, true), Times.Once);
        _personService.Verify(x => x.MatchEpisode(episode, false), Times.Never);
    }

    private static Episode CreateEpisode(string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Description mentioning someone else",
            ReleaseUtc = DateTime.UtcNow,
            Length = TimeSpan.FromMinutes(30),
            Explicit = false
        };
}
