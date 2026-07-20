using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Apple.Mapping;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.Mapping;

public class AppleCatalogueInputMappingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeFromCandidateFactory _factory = new();

    [Fact(DisplayName =
        "When an Apple API episode is mapped through catalogue input, adapter, and factory, " +
        "the episode matches the legacy FromApple shape because provider boundaries must preserve indexed fields.")]
    public void Apple_api_round_trip_matches_legacy_episode_shape()
    {
        // Arrange
        var input = _fixture.CreateAppleCatalogueInput();
        var appleEpisode = new AppleEpisode(
            input.AppleId,
            input.Title,
            input.Release,
            input.Duration,
            input.AppleUrl,
            input.Description,
            Explicit: false,
            Image: input.Image);
        // Act
        var mapped = appleEpisode.ToCatalogueInput();
        var candidate = new AppleEpisodeAdapter().Adapt(mapped);
        var episode = _factory.Create(candidate, explicitContent: false);

        // Assert
        episode.ShouldMatchExpectation(EpisodeExpectation.From(candidate));
    }

    [Fact(DisplayName =
        "When Apple episode title or description has surrounding whitespace, catalogue mapping trims both " +
        "because legacy FromApple used trimmed values.")]
    public void Title_and_description_whitespace_is_trimmed()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var input = _fixture.CreateAppleCatalogueInput();
        var appleEpisode = new AppleEpisode(
            input.AppleId,
            $"  {title}  ",
            input.Release,
            input.Duration,
            input.AppleUrl,
            $"  {description}  ",
            Explicit: false,
            Image: input.Image);

        // Act
        var mapped = appleEpisode.ToCatalogueInput();

        // Assert
        mapped.Title.Should().Be(title);
        mapped.Description.Should().Be(description);
    }
}
