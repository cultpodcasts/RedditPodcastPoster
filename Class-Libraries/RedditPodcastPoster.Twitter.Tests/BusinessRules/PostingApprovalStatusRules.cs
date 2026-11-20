using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.SocialPosting.Episodes;

namespace RedditPodcastPoster.Twitter.Tests.BusinessRules;

/// <summary>
/// #1484: the posting jobs (Reddit, X, Bluesky) key off an episode's approval status: an episode awaiting approval
/// or rejected is never posted, whatever its removed and posting flags say; an approved one posts under the usual rules.
/// </summary>
public class PostingApprovalStatusRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly Episode _ordinary;
    private readonly Episode _awaiting;
    private readonly Episode _rejected;
    private readonly Episode _approved;

    public PostingApprovalStatusRules()
    {
        _mocker.Use(Options.Create(new DelayedYouTubePublication()));
        _podcast = _fixture.CreatePodcast();
        _ordinary = Postable();
        _awaiting = SplitOff();
        _rejected = SplitOff();
        EpisodeServiceLinks.Reject(_rejected);
        _approved = SplitOff();
        EpisodeServiceLinks.Approve(_approved);
        // Prove posting keys off the status, not the removed flag that also hides a held split-off.
        _awaiting.Removed = false;
        _rejected.Removed = false;
    }

    private Episode Postable()
    {
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        episode.Posted = false;
        episode.Ignored = false;
        episode.Removed = false;
        episode.TweetPost = null;
        episode.TweetSkipReason = null;
        episode.OldTweeted = null;
        episode.BlueskyPost = null;
        episode.OldBlueskyPosted = null;
        return episode;
    }

    private Episode SplitOff()
    {
        // The split-off takes the source's Spotify link, so it has a postable url.
        return EpisodeServiceLinks.SplitOff(Postable(), [ServiceKeys.Spotify]).Episode;
    }

    private Episode[] All => [_ordinary, _awaiting, _rejected, _approved];

    [Fact(DisplayName =
        "Reddit posting skips an episode awaiting approval or rejected, and posts an approved split-off like an " +
        "ordinary episode (#1484).")]
    public async Task reddit_posting_keys_off_approval_status()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastEpisodeFilter>();
        var candidates = All.Select(e => new PodcastEpisode(_podcast, e)).ToArray();

        // Act
        var result = await sut.GetNewEpisodesReleasedSince(candidates, DateTime.UtcNow.AddDays(-7), true, true);

        // Assert
        result.Select(x => x.Episode).Should().BeEquivalentTo([_ordinary, _approved]);
    }

    [Fact(DisplayName =
        "X posting skips an episode awaiting approval or rejected, and posts an approved split-off like an ordinary " +
        "episode (#1484).")]
    public async Task x_posting_keys_off_approval_status()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastEpisodeFilter>();

        // Act
        var result = await sut.GetMostRecentUntweetedEpisodes(_podcast, All, 7);

        // Assert
        result.Select(x => x.Episode).Should().BeEquivalentTo([_ordinary, _approved]);
    }

    [Fact(DisplayName =
        "Bluesky posting skips an episode awaiting approval or rejected, and posts an approved split-off like an " +
        "ordinary episode (#1484).")]
    public async Task bluesky_posting_keys_off_approval_status()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastEpisodeFilter>();

        // Act
        var result = await sut.GetMostRecentBlueskyReadyEpisodes(_podcast, All, 7);

        // Assert
        result.Select(x => x.Episode).Should().BeEquivalentTo([_ordinary, _approved]);
    }
}
