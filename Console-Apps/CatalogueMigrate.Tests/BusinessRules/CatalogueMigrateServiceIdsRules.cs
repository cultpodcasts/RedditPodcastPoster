using CatalogueMigrate;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace CatalogueMigrate.Tests.BusinessRules;

public class CatalogueMigrateServiceIdsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Service-id guard: when the destination parent document does not carry the podcast's YouTube channel, Spotify " +
        "and Apple ids, then each one is reported dropped, because the writer must refuse a move that loses them.")]
    public void ids_missing_from_the_parent_are_dropped()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.AppleId = _fixture.CreateAppleId();
        });
        var parent = new { name = podcast.Name };

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, parent);

        // Assert
        dropped.Should().BeEquivalentTo("youTubeChannelId", "spotifyId", "appleId");
    }

    [Fact(DisplayName =
        "Service-id guard: ids carried anywhere in the parent document (e.g. under indexing) are not dropped, because " +
        "the guard checks what the writer stores, whatever its shape.")]
    public void ids_nested_in_the_parent_are_kept()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.AppleId = _fixture.CreateAppleId();
        });
        var parent = new
        {
            name = podcast.Name,
            indexing = new { youTubeChannelId = podcast.YouTubeChannelId, appleId = podcast.AppleId }
        };

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, parent);

        // Assert
        dropped.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Service-id guard: a podcast with no service ids drops nothing, because there is nothing to keep.")]
    public void podcast_without_ids_drops_nothing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.AppleId = null);

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, new { name = podcast.Name });

        // Assert
        dropped.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Service-id guard: the id must match, not just the member name, because a parent carrying a different channel " +
        "id still loses the podcast's own.")]
    public void a_different_value_counts_as_dropped()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = _fixture.CreateYouTubeChannelId());
        var parent = new { indexing = new { youTubeChannelId = _fixture.CreateYouTubeChannelId() } };

        // Act
        var dropped = CatalogueMigrateServiceIds.Dropped(podcast, parent);

        // Assert
        dropped.Should().Equal("youTubeChannelId");
    }
}
