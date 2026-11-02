using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

/// <summary>
/// X post state matches Bluesky: legacy <c>tweeted</c> bool vs stored post id <c>tweetPost</c>.
/// A new post sets <see cref="Models.Episodes.Episode.TweetPost"/> and leaves
/// <see cref="Models.Episodes.Episode.OldTweeted"/> unset.
/// </summary>
public class EpisodeTweetPostStateRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Plain English rule: when TweetPost is set, then Tweeted is true and the legacy flag stays unset, because the stored id proves a network post.")]
    public void tweet_post_means_posted()
    {
        // Arrange
        var tweetPost = _fixture.Create<string>();
        var episode = _fixture.CreateEpisode(e => e.TweetPost = tweetPost);

        // Act
        var posted = episode.Tweeted;

        // Assert
        posted.Should().BeTrue();
        episode.OldTweeted.Should().BeNull();
        episode.TweetPost.Should().Be(tweetPost);
    }

    [Fact(DisplayName =
        "Plain English rule: when only legacy OldTweeted is true, then Tweeted is true, because documents posted before the id was stored remain posted.")]
    public void legacy_old_tweeted_means_posted()
    {
        // Arrange
        var episode = _fixture.CreateEpisode(e => e.OldTweeted = true);

        // Act
        var posted = episode.Tweeted;

        // Assert
        posted.Should().BeTrue();
        episode.TweetPost.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when neither legacy flag nor TweetPost is set, then Tweeted is false, because the episode has not been posted.")]
    public void unset_means_not_posted()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();

        // Act
        var posted = episode.Tweeted;

        // Assert
        posted.Should().BeFalse();
        episode.OldTweeted.Should().BeNull();
        episode.TweetPost.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when ClearTweetState runs, then both the legacy flag and TweetPost are cleared, because un-tweet must drop every posted marker.")]
    public void clear_tweet_state_clears_both_fields()
    {
        // Arrange
        var episode = _fixture.CreateEpisode(e =>
        {
            e.OldTweeted = true;
            e.TweetPost = _fixture.Create<string>();
        });

        // Act
        episode.ClearTweetState();

        // Assert
        episode.Tweeted.Should().BeFalse();
        episode.OldTweeted.Should().BeNull();
        episode.TweetPost.Should().BeNull();
    }
}
