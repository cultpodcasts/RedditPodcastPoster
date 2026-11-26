using FindDuplicateEpisodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace FindDuplicateEpisodes.Tests.BusinessRules;

/// <summary>
/// #1490: a split-off from <see cref="EpisodeServiceLinks.SplitOff"/> must not be deduped against its source.
/// </summary>
public class SplitOffDuplicatePairRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Podcast _podcast;

    public SplitOffDuplicatePairRules()
    {
        _podcast = _fixture.CreatePodcast();
    }

    [Theory(DisplayName =
        "When a partly saved overwrite leaves a split-off and its source sharing the Spotify id that named the " +
        "split-off, then the pair is not a duplicate whichever side is listed first, because merging would copy the " +
        "old links back and could publish or delete the split-off (#1490).")]
    [InlineData(false)]
    [InlineData(true)]
    public void partly_saved_split_off_sharing_spotify_id_is_not_a_duplicate(bool splitOffListedFirst)
    {
        // Arrange
        var (source, splitOff) = PartlySavedSplitOff();
        EpisodeServiceLinks.Approve(splitOff);
        var (first, second) = splitOffListedFirst ? (splitOff, source) : (source, splitOff);

        // Act
        var skip = FindDuplicateEpisodesProcessor.ShouldSkipDuplicatePair(first, second);

        // Assert
        splitOff.IsHeldForApproval().Should().BeFalse();
        splitOff.Id.Should().Be(EpisodeServiceLinks.SplitOffId(source));
        EpisodeServicePresence.SpotifyEpisodeId(splitOff).Should().Be(EpisodeServicePresence.SpotifyEpisodeId(source));
        skip.Should().BeTrue();
        splitOff.IsRemoved().Should().BeFalse();
        source.IsRemoved().Should().BeFalse();
    }

    [Theory(DisplayName =
        "When either episode is held for approval, then the pair is not a duplicate, because a merge must not clear " +
        "Removed or drop approvalStatus and publish the held episode (#1490).")]
    [InlineData(ApprovalStatus.AwaitingApproval, false)]
    [InlineData(ApprovalStatus.AwaitingApproval, true)]
    [InlineData(ApprovalStatus.Rejected, false)]
    [InlineData(ApprovalStatus.Rejected, true)]
    public void held_episode_is_not_a_duplicate_candidate(ApprovalStatus status, bool holdFirst)
    {
        // Arrange
        var first = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var second = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var held = holdFirst ? first : second;
        held.ApprovalStatus = status;

        // Act
        var skip = FindDuplicateEpisodesProcessor.ShouldSkipDuplicatePair(first, second);

        // Assert
        held.IsHeldForApproval().Should().BeTrue();
        held.ApprovalStatus.Should().Be(status);
        skip.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When an unapproved split-off has already taken the old links, then the pair is still not a duplicate, " +
        "because the split-off stays held and a merge must not set its Removed flag back to false (#1490).")]
    public void unapproved_split_off_stays_held_and_is_not_a_duplicate()
    {
        // Arrange
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var splitOff = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;
        var replacementId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(source, replacementId);
        EpisodeServicePresence.Upsert(source, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(replacementId), null);

        // Act
        var skip = FindDuplicateEpisodesProcessor.ShouldSkipDuplicatePair(source, splitOff);

        // Assert
        splitOff.IsHeldForApproval().Should().BeTrue();
        splitOff.IsRemoved().Should().BeTrue();
        skip.Should().BeTrue();
        splitOff.Removed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a split-off has been approved and the source now carries different service links, then the pair is a " +
        "normal duplicate candidate, because a finished overwrite no longer shares the id that named the split-off (#1490).")]
    public void approved_split_off_with_finished_overwrite_is_not_a_split_off_pair()
    {
        // Arrange
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var splitOff = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;
        var replacementId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(source, replacementId);
        EpisodeServicePresence.Upsert(source, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(replacementId), null);
        EpisodeServiceLinks.Approve(splitOff);

        // Act
        var skip = FindDuplicateEpisodesProcessor.ShouldSkipDuplicatePair(source, splitOff);

        // Assert
        splitOff.IsHeldForApproval().Should().BeFalse();
        splitOff.Id.Should().NotBe(EpisodeServiceLinks.SplitOffId(source));
        EpisodeServicePresence.SpotifyEpisodeId(splitOff).Should()
            .NotBe(EpisodeServicePresence.SpotifyEpisodeId(source));
        skip.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When two ordinary episodes share a Spotify id and neither is held or a split-off of the other, then they " +
        "remain duplicate candidates, because only a known split-off relationship is skipped (#1490).")]
    public void ordinary_episodes_sharing_spotify_id_remain_duplicate_candidates()
    {
        // Arrange
        var first = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var second = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var sharedId = EpisodeServicePresence.SpotifyEpisodeId(first);
        EpisodeServicePresence.SetSpotifyIdentity(second, sharedId);
        EpisodeServicePresence.Upsert(
            second, ServiceKeys.Spotify, EpisodeServicePresence.TryGetUrl(first, ServiceKeys.Spotify), null);

        // Act
        var skip = FindDuplicateEpisodesProcessor.ShouldSkipDuplicatePair(first, second);

        // Assert
        first.IsHeldForApproval().Should().BeFalse();
        second.IsHeldForApproval().Should().BeFalse();
        EpisodeServicePresence.SpotifyEpisodeId(first).Should().Be(EpisodeServicePresence.SpotifyEpisodeId(second));
        skip.Should().BeFalse();
    }

    private (Episode Source, Episode SplitOff) PartlySavedSplitOff()
    {
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(source);
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(source, ServiceKeys.Spotify);
        var splitOff = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;
        EpisodeServicePresence.SetSpotifyIdentity(source, spotifyId);
        EpisodeServicePresence.Upsert(source, ServiceKeys.Spotify, spotifyUrl, null);
        return (source, splitOff);
    }
}
