using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using Azure.Search.Documents;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services;

public class EpisodeUpdateServiceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PodcastEpisodeResolverResponse _resolved =
        new(null, null, PodcastEpisodeResolveState.PodcastNotFound);
    private Episode? _saved;
    private string? _uriSeenByRemove;
    private Func<Task<EntitySearchIndexerResponse>> _index = () =>
        Task.FromResult(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

    public EpisodeUpdateServiceTests()
    {
        _mocker.Use(new EpisodeChangeApplier(NullLogger<EpisodeChangeApplier>.Instance));
        _mocker.Use(new EpisodeSearchIndexCleanup(
            CreateUninitializedSearchClient(),
            NullLogger<EpisodeSearchIndexCleanup>.Instance));
        _mocker.Use(NullLogger<EpisodeUpdateService>.Instance);
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(() => _resolved);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .Callback<Episode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisode(It.IsAny<Podcast>(), It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns((Podcast _, Episode _, CancellationToken _) => _index());
    }

    [Fact(DisplayName =
        "Plain English rule: when the episode is not found, then return NotFound and do not save, because there is nothing to update.")]
    public async Task update_returns_not_found_and_does_not_save_when_episode_missing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                _fixture.CreateGuid(),
                _fixture.CreateGuid(),
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when the podcast is not found for a resolved episode, then return NotFound and do not save, because the episode cannot be updated without its podcast.")]
    public async Task update_returns_not_found_and_does_not_save_when_podcast_missing()
    {
        // Arrange
        var episode = OldEpisode();
        _resolved = new PodcastEpisodeResolverResponse(episode, null, PodcastEpisodeResolveState.Resolved);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                episode.PodcastId,
                episode.Id,
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when a title-only change is accepted, then save the episode once, because persistence is the core update outcome.")]
    public async Task update_happy_path_saves_episode_once()
    {
        // Arrange
        var (episode, podcast) = ResolvedOldPair();
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved.Should().Be(episode);
    }

    [Fact(DisplayName =
        "Plain English rule: when the change request does not untweet or unbluesky, then social remove managers are not called, because those side effects are flag-driven.")]
    public async Task update_without_unsocial_flags_does_not_call_social_managers()
    {
        // Arrange
        var (episode, podcast) = ResolvedOldPair(e =>
        {
            e.OldTweeted = true;
            e.BlueskyPost = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        });
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _mocker.GetMock<ITweetManager>().Verify(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()), Times.Never);
        _mocker.GetMock<IBlueskyPostManager>().Verify(m => m.RemovePost(It.IsAny<PodcastEpisode>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when UnBluesky succeeds, then RemovePost sees the AT URI on the episode and Cosmostate is cleared before Save, because delete-before-clear keeps retry possible on failure.")]
    public async Task update_unbluesky_deletes_then_clears_on_success()
    {
        // Arrange
        const string atUri = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        var (episode, podcast) = ResolvedOldPair(e => e.BlueskyPost = atUri);
        _mocker.GetMock<IBlueskyPostManager>()
            .Setup(m => m.RemovePost(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(pe => _uriSeenByRemove = pe.Episode.BlueskyPost)
            .ReturnsAsync(RemovePostState.Deleted);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { UnBluesky = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        result.Outcome!.BlueskyPostDeleted.Should().BeTrue();
        _uriSeenByRemove.Should().Be(atUri);
        episode.BlueskyPost.Should().BeNull();
        _mocker.GetMock<IBlueskyPostManager>().Verify(m => m.RemovePost(It.IsAny<PodcastEpisode>()), Times.Once);
    }

    [Fact(DisplayName =
        "Plain English rule: when UnBluesky delete fails, then Cosmostate keeps BlueskyPost, because a failed remote delete must not flip posted state and block retries.")]
    public async Task update_unbluesky_keeps_at_uri_when_delete_fails()
    {
        // Arrange
        const string atUri = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        var (episode, podcast) = ResolvedOldPair(e => e.BlueskyPost = atUri);
        _mocker.GetMock<IBlueskyPostManager>()
            .Setup(m => m.RemovePost(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(RemovePostState.Other);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { UnBluesky = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        result.Outcome!.BlueskyPostDeleted.Should().BeFalse();
        episode.BlueskyPost.Should().Be(atUri);
        episode.BlueskyPosted.Should().BeTrue();
    }

    [Theory(DisplayName =
        "Plain English rule: when an un-tweet finds the tweet gone (deleted now or already absent), then the tweet manager sees the stored tweet id and Tweeted and the id are cleared before Save (#1204).")]
    [InlineData(RemoveTweetState.Deleted)]
    [InlineData(RemoveTweetState.NotFound)]
    public async Task update_untweet_deletes_then_clears_when_gone(RemoveTweetState state)
    {
        // Arrange
        const string tweetId = "1234567890123456789";
        var (episode, podcast) = ResolvedOldPair(e =>
        {
            e.OldTweeted = true;
            e.TweetPost = tweetId;
        });
        string? idSeenByRemove = null;
        var tweetedSeenByRemove = false;
        bool? tweetedAtSave = null;
        _mocker.GetMock<ITweetManager>()
            .Setup(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(pe =>
            {
                idSeenByRemove = pe.Episode.TweetPost;
                tweetedSeenByRemove = pe.Episode.Tweeted;
            })
            .ReturnsAsync(state);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .Callback<Episode>(saved => tweetedAtSave = saved.Tweeted)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id, new EpisodeChangeRequest { Tweeted = false }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        result.Outcome!.TweetDeleted.Should().BeTrue();
        idSeenByRemove.Should().Be(tweetId);
        tweetedSeenByRemove.Should().BeTrue();
        tweetedAtSave.Should().BeFalse();
        episode.Tweeted.Should().BeFalse();
        episode.TweetPost.Should().BeNull();
    }

    [Theory(DisplayName =
        "Plain English rule: when an un-tweet fails, then the episode is saved still tweeted with its tweet id, because clearing it would let the hourly job re-post while the old tweet is live (#1204).")]
    [InlineData(RemoveTweetState.Other)]
    [InlineData(RemoveTweetState.TooManyRequests)]
    public async Task update_untweet_keeps_tweeted_when_delete_fails(RemoveTweetState state)
    {
        // Arrange
        const string tweetId = "1234567890123456789";
        var (episode, podcast) = ResolvedOldPair(e =>
        {
            e.OldTweeted = true;
            e.TweetPost = tweetId;
        });
        _mocker.GetMock<ITweetManager>()
            .Setup(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(state);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id, new EpisodeChangeRequest { Tweeted = false }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        result.Outcome!.TweetDeleted.Should().BeFalse();
        _saved.Should().Be(episode);
        episode.Tweeted.Should().BeTrue();
        episode.TweetPost.Should().Be(tweetId);
    }

    [Fact(DisplayName =
        "Plain English rule: when one request corrects the release and duration and un-tweets an episode with no stored tweet id, then the tweet search sees the old release and duration, because the live post was written with them (#1204).")]
    public async Task update_untweet_searches_with_pre_change_release_and_length()
    {
        // Arrange
        var oldRelease = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(7));
        var oldLength = TimeSpan.FromMinutes(41);
        var (episode, podcast) = ResolvedOldPair(e =>
        {
            e.OldTweeted = true;
            e.TweetPost = null;
            e.ReleaseUtc = oldRelease;
            e.Length = oldLength;
        });
        var newRelease = DomainTestFixture.UtcAtTime(-4, TimeSpan.FromHours(10));
        PodcastEpisode? seenByRemove = null;
        _mocker.GetMock<ITweetManager>()
            .Setup(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(pe => seenByRemove = pe)
            .ReturnsAsync(RemoveTweetState.Deleted);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id,
                new EpisodeChangeRequest { Tweeted = false, Release = newRelease, Duration = "00:52:00" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        seenByRemove.Should().NotBeNull();
        seenByRemove!.Episode.Should().NotBeSameAs(episode);
        seenByRemove.Episode.Id.Should().Be(episode.Id);
        seenByRemove.Episode.ReleaseUtc.Should().Be(oldRelease);
        seenByRemove.Episode.Length.Should().Be(oldLength);
        seenByRemove.Episode.Tweeted.Should().BeTrue();
        episode.ReleaseUtc.Should().Be(newRelease);
        episode.Length.Should().Be(TimeSpan.FromMinutes(52));
        episode.Tweeted.Should().BeFalse();
        episode.TweetPost.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when the save fails after a successful tweet delete, then the update fails and nothing is stored, so the stored episode keeps Tweeted and its id and the next un-tweet by id gets NotFound and clears it.")]
    public async Task update_untweet_save_failure_after_delete_fails_the_update()
    {
        // Arrange
        const string tweetId = "1234567890123456789";
        var (episode, podcast) = ResolvedOldPair(e =>
        {
            e.OldTweeted = true;
            e.TweetPost = tweetId;
        });
        _mocker.GetMock<ITweetManager>()
            .Setup(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(RemoveTweetState.Deleted);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .ThrowsAsync(new InvalidOperationException("synthetic save failure"));
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id, new EpisodeChangeRequest { Tweeted = false }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Failed);
        _mocker.GetMock<ITweetManager>().Verify(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.Save(It.IsAny<Episode>()), Times.Once);
    }

    [Fact(DisplayName =
        "Plain English rule: when the tweet manager throws during an un-tweet, then the episode is still saved and stays tweeted, because an unknown result must not forget a live tweet.")]
    public async Task update_untweet_keeps_tweeted_when_remove_throws()
    {
        // Arrange
        var (episode, podcast) = ResolvedOldPair(e => e.OldTweeted = true);
        _mocker.GetMock<ITweetManager>()
            .Setup(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()))
            .ThrowsAsync(new InvalidOperationException("synthetic"));
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id, new EpisodeChangeRequest { Tweeted = false }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        result.Outcome!.TweetDeleted.Should().BeFalse();
        _saved.Should().Be(episode);
        episode.Tweeted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When search indexing throws after Cosmos save, then the update is still Accepted, because a curator guests POST must not 500 after the document is persisted.")]
    public async Task update_accepted_when_indexer_throws_after_save()
    {
        // Arrange
        var guest = _fixture.CreateTitle();
        var (episode, podcast) = ResolvedOldPair();
        _index = () => throw new InvalidOperationException("search unavailable");
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { Guests = [guest] }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved.Should().Be(episode);
        episode.Guests.Should().Equal(guest);
    }

    [Fact(DisplayName =
        "Plain English rule: when a curator approves a split-off awaiting approval, then its approval status is cleared to null (approved) and it is saved public and indexed, with no posting flag set and nothing un-posted, because approval lets the normal posting jobs post it (#1484).")]
    public async Task update_approving_a_split_off_makes_it_public_and_postable()
    {
        // Arrange
        var (episode, podcast) = ResolvedSplitOffPair();
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id,
                new EpisodeChangeRequest { ApprovalDecision = ApprovalDecision.Approve }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved.Should().Be(episode);
        episode.ApprovalStatus.Should().BeNull();
        episode.Removed.Should().BeFalse();
        episode.Posted.Should().BeFalse();
        episode.TweetSkipReason.Should().BeNull();
        episode.BlueskyPosted.Should().BeFalse();
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(
            s => s.IndexEpisode(podcast, episode, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ITweetManager>().Verify(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()), Times.Never);
        _mocker.GetMock<IBlueskyPostManager>().Verify(m => m.RemovePost(It.IsAny<PodcastEpisode>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when a curator rejects a split-off awaiting approval, then it is saved Rejected and still removed and is not indexed, because a rejected split-off stays out of public (#1484).")]
    public async Task update_rejecting_a_split_off_keeps_it_hidden()
    {
        // Arrange
        var (episode, podcast) = ResolvedSplitOffPair();
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id,
                new EpisodeChangeRequest { ApprovalDecision = ApprovalDecision.Reject }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved.Should().Be(episode);
        episode.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
        episode.Removed.Should().BeTrue();
        episode.IsHeldForApproval().Should().BeTrue();
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(
            s => s.IndexEpisode(It.IsAny<Podcast>(), It.IsAny<Episode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName =
        "Plain English rule: a split-off held for approval is made public only by approving it, so clearing removed on one awaiting approval or rejected is a bad request and nothing is saved (#1484).")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task update_clearing_removed_on_a_held_split_off_is_rejected(bool rejected)
    {
        // Arrange
        var (episode, podcast) = ResolvedSplitOffPair();
        if (rejected)
        {
            EpisodeServiceLinks.Reject(episode);
        }

        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id, new EpisodeChangeRequest { Removed = false }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.BadRequest);
        result.Message.Should().NotBeNullOrWhiteSpace();
        _saved.Should().BeNull();
        episode.Removed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Plain English rule: an approved split-off later removed (e.g. unplayable) can be un-removed like any episode, because it is no longer held for approval (#1484).")]
    public async Task update_clearing_removed_on_an_approved_split_off_is_accepted()
    {
        // Arrange
        var (episode, podcast) = ResolvedSplitOffPair();
        EpisodeServiceLinks.Approve(episode);
        episode.Removed = true;
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id, new EpisodeChangeRequest { Removed = false }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        episode.Removed.Should().BeFalse();
        episode.ApprovalStatus.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: only an episode held for approval can be rejected (no status means approved, like any ordinary episode), and approve or reject cannot be combined with the opposite removed, so each is a bad request and nothing is saved (#1484).")]
    public async Task update_invalid_split_off_review_is_a_bad_request()
    {
        // Arrange
        var (ordinary, ordinaryPodcast) = ResolvedOldPair();
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var rejectOrdinary = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(ordinaryPodcast.Id, ordinary.Id,
                new EpisodeChangeRequest { ApprovalDecision = ApprovalDecision.Reject }),
            CancellationToken.None);
        var (splitOff, podcast) = ResolvedSplitOffPair();
        var approveAndRemove = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, splitOff.Id,
                new EpisodeChangeRequest { ApprovalDecision = ApprovalDecision.Approve, Removed = true }),
            CancellationToken.None);
        var rejectAndUnremove = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, splitOff.Id,
                new EpisodeChangeRequest { ApprovalDecision = ApprovalDecision.Reject, Removed = false }),
            CancellationToken.None);

        // Assert
        rejectOrdinary.Status.Should().Be(EpisodeUpdateStatus.BadRequest);
        approveAndRemove.Status.Should().Be(EpisodeUpdateStatus.BadRequest);
        rejectAndUnremove.Status.Should().Be(EpisodeUpdateStatus.BadRequest);
        _saved.Should().BeNull();
        ordinary.ApprovalStatus.Should().BeNull();
        splitOff.ApprovalStatus.Should().Be(ApprovalStatus.AwaitingApproval);
    }

    [Fact(DisplayName =
        "Plain English rule: approving an ordinary episode (no status, already approved) is accepted and leaves it approved and public (#1484).")]
    public async Task update_approving_an_ordinary_episode_changes_nothing()
    {
        // Arrange
        var (ordinary, podcast) = ResolvedOldPair(e => e.Removed = false);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, ordinary.Id,
                new EpisodeChangeRequest { ApprovalDecision = ApprovalDecision.Approve }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        ordinary.ApprovalStatus.Should().BeNull();
        ordinary.Removed.Should().BeFalse();
    }

    private (Episode Episode, Podcast Podcast) ResolvedSplitOffPair()
    {
        var source = OldEpisode(e =>
        {
            var spotifyId = _fixture.CreateSpotifyId();
            EpisodeServicePresence.SetSpotifyIdentity(e, spotifyId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(spotifyId), null);
        });
        var splitOff = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;
        var podcast = _fixture.CreatePodcast(p => p.Id = splitOff.PodcastId);
        _resolved = new PodcastEpisodeResolverResponse(splitOff, podcast, PodcastEpisodeResolveState.Resolved);
        return (splitOff, podcast);
    }

    private (Episode Episode, Podcast Podcast) ResolvedOldPair(Action<Episode>? customize = null)
    {
        var episode = OldEpisode(customize);
        var podcast = _fixture.CreatePodcast(p => p.Id = episode.PodcastId);
        _resolved = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
        return (episode, podcast);
    }

    private Episode OldEpisode(Action<Episode>? customize = null) =>
        _fixture.CreateEpisode(e =>
        {
            e.ReleaseUtc = DateTime.UtcNow.AddDays(-30);
            customize?.Invoke(e);
        });

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
