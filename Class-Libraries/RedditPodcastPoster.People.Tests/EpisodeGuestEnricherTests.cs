using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Models;
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
        var episode = CreateEpisode("Interview with Janja Lalich");
        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Janja Lalich", null, null),
            [new PersonMatchResult("Janja Lalich", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().Equal("Janja Lalich");
        result.SkippedLowConfidence.Should().BeEmpty();
        episode.Guests.Should().Equal("Janja Lalich");
    }

    [Fact]
    public async Task EnrichGuests_ShortTerm_SkippedAsLowConfidence()
    {
        var episode = CreateEpisode("Chat with Jon about cults");
        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Jon", null, null),
            [new PersonMatchResult("Jon", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().BeEmpty();
        result.SkippedLowConfidence.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Jon");
        episode.Guests.Should().BeNull();
    }

    [Fact]
    public async Task EnrichGuests_NeverRemovesExistingGuests()
    {
        var episode = CreateEpisode("Interview with Janja Lalich");
        episode.Guests = ["Existing Guest"];

        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Janja Lalich", null, null),
            [new PersonMatchResult("Janja Lalich", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().Equal("Janja Lalich");
        episode.Guests.Should().BeEquivalentTo(["Existing Guest", "Janja Lalich"]);
    }

    [Fact]
    public async Task EnrichGuests_DoesNotDuplicateExistingGuest_CaseInsensitive()
    {
        var episode = CreateEpisode("Interview with Janja Lalich");
        episode.Guests = ["janja lalich"];

        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Janja Lalich", null, null),
            [new PersonMatchResult("Janja Lalich", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        var result = await CreateSut().EnrichGuests(episode);

        result.Additions.Should().BeEmpty();
        episode.Guests.Should().Equal("janja lalich");
    }

    [Fact]
    public async Task EnrichGuests_NeverTouchesHandleFields()
    {
        var episode = CreateEpisode("Interview with Janja Lalich");
        episode.TwitterHandles = ["@existing"];
        episode.BlueskyHandles = ["existing.bsky.social"];

        var match = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Janja Lalich", "@janja", "janja.bsky.social"),
            [new PersonMatchResult("Janja Lalich", 1)]);

        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([match]);

        await CreateSut().EnrichGuests(episode);

        episode.Guests.Should().Equal("Janja Lalich");
        episode.TwitterHandles.Should().Equal("@existing");
        episode.BlueskyHandles.Should().Equal("existing.bsky.social");
    }

    [Fact]
    public async Task EnrichGuests_TitleOnly_PassesWithDescriptionFalse()
    {
        var episode = CreateEpisode("Some title");
        _personService
            .Setup(x => x.MatchEpisode(episode, false))
            .ReturnsAsync([]);

        await CreateSut().EnrichGuests(episode, GuestEnrichmentOptions.Default);

        _personService.Verify(x => x.MatchEpisode(episode, false), Times.Once);
        _personService.Verify(x => x.MatchEpisode(episode, true), Times.Never);
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
