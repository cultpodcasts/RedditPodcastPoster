using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Apple.Mapping;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.Mapping;

public class AppleCatalogueInputMappingRules
{
    private readonly DomainTestFixture _fixture = new();

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
