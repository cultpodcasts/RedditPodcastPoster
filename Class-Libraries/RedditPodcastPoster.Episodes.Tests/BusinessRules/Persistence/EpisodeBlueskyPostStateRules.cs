using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

/// <summary>
/// Bluesky post state: legacy <c>bluesky</c> bool vs stored AT URI <c>blueskyPost</c>.
/// Application code must set <see cref="Models.Episodes.Episode.BlueskyPost"/> after posting,
/// never write <see cref="Models.Episodes.Episode.OldBlueskyPosted"/> to true.
/// </summary>
public class EpisodeBlueskyPostStateRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Plain English rule: when BlueskyPost AT URI is set, then BlueskyPosted is true, because the stored URI proves a network post.")]
    public void bluesky_post_uri_means_posted()
    {
        // Arrange
        var episode = _fixture.CreateEpisode(e =>
            e.BlueskyPost = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2");

        // Act
        var posted = episode.BlueskyPosted;

        // Assert
        posted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Plain English rule: when only legacy OldBlueskyPosted is true, then BlueskyPosted is true, because pre-migration documents remain posted.")]
    public void legacy_old_bluesky_posted_means_posted()
    {
        // Arrange
        var episode = _fixture.CreateEpisode(e => e.OldBlueskyPosted = true);

        // Act
        var posted = episode.BlueskyPosted;

        // Assert
        posted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Plain English rule: when neither legacy flag nor BlueskyPost is set, then BlueskyPosted is false, because the episode has not been posted.")]
    public void unset_means_not_posted()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();

        // Act
        var posted = episode.BlueskyPosted;

        // Assert
        posted.Should().BeFalse();
        episode.OldBlueskyPosted.Should().BeNull();
        episode.BlueskyPost.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when ClearBlueskyPostState runs, then both legacy flag and BlueskyPost are cleared, because un-post must drop all posted markers.")]
    public void clear_bluesky_post_state_clears_both_fields()
    {
        // Arrange
        var episode = _fixture.CreateEpisode(e =>
        {
            e.OldBlueskyPosted = true;
            e.BlueskyPost = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        });

        // Act
        episode.ClearBlueskyPostState();

        // Assert
        episode.BlueskyPosted.Should().BeFalse();
        episode.OldBlueskyPosted.Should().BeNull();
        episode.BlueskyPost.Should().BeNull();
    }
}
