using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

/// <summary>
/// Film X post state mirrors Playable: skip reason alone is done for posting (#1209).
/// </summary>
public class FilmTweetPostStateRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Plain English rule: when a Film has only TweetSkipReason set, then Tweeted is true and ClearTweetState clears the reason, because a permanent form failure must not be retried and un-tweet must drop the skip marker.")]
    public void film_tweet_skip_reason_means_tweeted_and_clear_drops_it()
    {
        // Arrange
        var reason = _fixture.Create<string>();
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            TweetSkipReason = reason,
            OldTweeted = null,
            TweetPost = null
        };

        // Act
        var tweetedBeforeClear = film.Tweeted;
        film.ClearTweetState();

        // Assert
        tweetedBeforeClear.Should().BeTrue();
        film.Tweeted.Should().BeFalse();
        film.TweetSkipReason.Should().BeNull();
        film.OldTweeted.Should().BeNull();
        film.TweetPost.Should().BeNull();
    }
}
