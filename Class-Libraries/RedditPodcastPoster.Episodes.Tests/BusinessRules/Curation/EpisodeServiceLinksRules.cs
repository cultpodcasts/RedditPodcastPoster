using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Curation;

/// <summary>
/// #1416 / #1471: the curator operation that moves service links between podcast episodes. Submit's
/// overwrite-and-split uses it to keep the stored links on a new episode; the planned duplicate merge reuses it.
/// </summary>
public class EpisodeServiceLinksRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Podcast _podcast;

    public EpisodeServiceLinksRules()
    {
        _podcast = _fixture.CreatePodcast();
    }

    [Fact(DisplayName =
        "Moving the Spotify and Apple links carries each id, url and image to the target and clears them from the " +
        "source, so the link lives on exactly one episode.")]
    public void move_carries_id_url_and_image_and_clears_source()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var spotifyImage = _fixture.DefaultSpotifyImage(spotifyId);
        var source = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .WithSpotifyImage(spotifyImage)
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .Create();
        var target = _fixture.BuildEpisode().WithPodcast(_podcast).Create();

        // Act
        var result = EpisodeServiceLinks.Move(source, target, [ServiceKeys.Spotify, ServiceKeys.Apple]);

        // Assert
        result.Moved.Should().BeEquivalentTo([ServiceKeys.Spotify, ServiceKeys.Apple]);
        result.Conflicting.Should().BeEmpty();
        EpisodeServicePresence.SpotifyEpisodeId(target).Should().Be(spotifyId);
        EpisodeServicePresence.TryGetUrl(target, ServiceKeys.Spotify).Should().Be(_fixture.DefaultSpotifyUrl(spotifyId));
        EpisodeServicePresence.TryGetImage(target, ServiceKeys.Spotify).Should().Be(spotifyImage);
        EpisodeServicePresence.AppleEpisodeId(target).Should().Be(appleId);
        EpisodeServicePresence.TryGetUrl(target, ServiceKeys.Apple).Should().Be(_fixture.DefaultAppleUrl(appleId));
        EpisodeServicePresence.SpotifyEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.HasUrl(source, ServiceKeys.Spotify).Should().BeFalse();
        EpisodeServicePresence.AppleEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.HasUrl(source, ServiceKeys.Apple).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Moving a link the target already holds with a different id leaves it on both episodes and reports it as " +
        "conflicting, because a move never overwrites or loses a link.")]
    public void move_reports_conflict_and_keeps_both_links()
    {
        // Arrange
        var sourceSpotifyId = _fixture.CreateSpotifyId();
        var targetSpotifyId = _fixture.CreateSpotifyId();
        var source = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithSpotify(sourceSpotifyId, _fixture.DefaultSpotifyUrl(sourceSpotifyId))
            .Create();
        var target = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithSpotify(targetSpotifyId, _fixture.DefaultSpotifyUrl(targetSpotifyId))
            .Create();

        // Act
        var result = EpisodeServiceLinks.Move(source, target, [ServiceKeys.Spotify]);

        // Assert
        result.Moved.Should().BeEmpty();
        result.Conflicting.Should().Equal(ServiceKeys.Spotify);
        EpisodeServicePresence.SpotifyEpisodeId(source).Should().Be(sourceSpotifyId);
        EpisodeServicePresence.SpotifyEpisodeId(target).Should().Be(targetSpotifyId);
    }

    [Fact(DisplayName =
        "Moving a link the target already holds with the same id clears it from the source, because the duplicate " +
        "copy is redundant once the target carries it.")]
    public void move_of_identical_link_clears_source()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var source = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
        var target = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();

        // Act
        var result = EpisodeServiceLinks.Move(source, target, [ServiceKeys.Spotify]);

        // Assert
        result.Moved.Should().Equal(ServiceKeys.Spotify);
        EpisodeServicePresence.SpotifyEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.SpotifyEpisodeId(target).Should().Be(spotifyId);
    }

    [Fact(DisplayName =
        "Moving a service the source does not carry changes nothing and is reported neither moved nor conflicting.")]
    public void move_ignores_absent_service()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var source = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast);
        var target = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .Create();

        // Act
        var result = EpisodeServiceLinks.Move(source, target, [ServiceKeys.Spotify]);

        // Assert
        result.Moved.Should().BeEmpty();
        result.Conflicting.Should().BeEmpty();
        EpisodeServicePresence.NestedYouTubeId(target).Should().Be(youTubeId);
    }

    [Fact(DisplayName =
        "Moving links onto the same episode is refused, because it would clear the links it was asked to keep.")]
    public void move_onto_same_episode_is_refused()
    {
        // Arrange
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);

        // Act
        var act = () => EpisodeServiceLinks.Move(episode, episode, [ServiceKeys.Spotify]);

        // Assert
        act.Should().Throw<ArgumentException>();
        EpisodeServicePresence.SpotifyEpisodeId(episode).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Splitting the Spotify and Apple links off an episode creates a new episode in the same podcast that holds " +
        "those links with the source's title, release and length, and leaves the source's YouTube link in place.")]
    public void split_off_moves_links_to_new_episode_in_same_podcast()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var youTubeId = _fixture.CreateYouTubeId();
        var source = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .Create();

        // Act
        var result = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify, ServiceKeys.Apple]);

        // Assert
        var split = result.Episode;
        split.Id.Should().NotBe(source.Id);
        split.PodcastId.Should().Be(source.PodcastId);
        split.Title.Should().Be(source.Title);
        split.ReleaseUtc.Should().Be(source.ReleaseUtc);
        split.Length.Should().Be(source.Length);
        EpisodeServicePresence.SpotifyEpisodeId(split).Should().Be(spotifyId);
        EpisodeServicePresence.AppleEpisodeId(split).Should().Be(appleId);
        EpisodeServicePresence.HasUrl(split, ServiceKeys.YouTube).Should().BeFalse();
        EpisodeServicePresence.SpotifyEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.AppleEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.NestedYouTubeId(source).Should().Be(youTubeId);
        result.Moved.Should().BeEquivalentTo([ServiceKeys.Spotify, ServiceKeys.Apple]);
    }

    [Fact(DisplayName =
        "A split-off awaits approval and is kept from posting by its approval status, not by posting flags: it carries " +
        "no posted, X skip or Bluesky flag and none of the source's post ids, so once approved it posts under the " +
        "usual rules (#1484).")]
    public void split_off_awaiting_approval_is_held_by_status_not_posting_flags()
    {
        // Arrange
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        source.Posted = true;

        // Act
        var split = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;

        // Assert
        split.IsHeldForApproval().Should().BeTrue();
        split.Posted.Should().BeFalse();
        split.Tweeted.Should().BeFalse();
        split.TweetSkipReason.Should().BeNull();
        split.BlueskyPosted.Should().BeFalse();
        split.TweetPost.Should().BeNull("the source's post ids must not be shared, or un-posting one would delete the other's post");
        split.BlueskyPost.Should().BeNull();
    }

    [Fact(DisplayName =
        "A split-off episode awaits approval and is hidden from the public until a curator approves it; approving it " +
        "makes it public (#1484).")]
    public void split_off_episode_is_hidden_until_approved()
    {
        // Arrange
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        source.Removed = false;

        // Act
        var split = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;

        // Assert
        split.Removed.Should().BeTrue();
        split.ApprovalStatus.Should().Be(ApprovalStatus.AwaitingApproval);
        source.ApprovalStatus.Should().BeNull();
        split.IsAwaitingApproval().Should().BeTrue();
        split.IsHeldForApproval().Should().BeTrue();
        source.IsAwaitingApproval().Should().BeFalse();
        EpisodeServiceLinks.Approve(split).Should().BeTrue();
        split.Removed.Should().BeFalse();
        split.IsAwaitingApproval().Should().BeFalse();
        split.IsHeldForApproval().Should().BeFalse();
    }

    [Fact(DisplayName =
        "Approving a split-off only changes its approval status and makes it public; no posting flag is touched, so " +
        "the normal posting jobs may post it under their usual rules (#1484).")]
    public void approving_a_split_off_makes_it_public_without_touching_posting_flags()
    {
        // Arrange
        var split = EpisodeServiceLinks.SplitOff(_fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast), [ServiceKeys.Spotify]).Episode;

        // Act
        var changed = EpisodeServiceLinks.Approve(split);

        // Assert
        changed.Should().BeTrue();
        split.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        split.Removed.Should().BeFalse();
        split.IsHeldForApproval().Should().BeFalse();
        split.Posted.Should().BeFalse();
        split.Tweeted.Should().BeFalse();
        split.BlueskyPosted.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Rejecting a split-off keeps it hidden and held from posting by its status; a rejected split-off is held but " +
        "no longer awaiting approval, and rejecting again changes nothing (#1484).")]
    public void rejecting_a_split_off_keeps_it_hidden_and_unposted()
    {
        // Arrange
        var split = EpisodeServiceLinks.SplitOff(_fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast), [ServiceKeys.Spotify]).Episode;

        // Act
        var changed = EpisodeServiceLinks.Reject(split);

        // Assert
        changed.Should().BeTrue();
        split.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
        split.Removed.Should().BeTrue();
        split.IsAwaitingApproval().Should().BeFalse();
        split.IsHeldForApproval().Should().BeTrue();
        EpisodeServiceLinks.Reject(split).Should().BeFalse("rejecting again changes nothing");
    }

    [Fact(DisplayName =
        "The approval status is stored as the enum name (AwaitingApproval / Approved / Rejected), like the other stored " +
        "enums, so it can be queried, and is omitted on an ordinary episode (#1484).")]
    public void approval_status_is_stored_as_the_enum_name()
    {
        // Arrange
        var split = EpisodeServiceLinks.SplitOff(_fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast), [ServiceKeys.Spotify]).Episode;
        var ordinary = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);

        // Act
        var splitJson = System.Text.Json.JsonSerializer.Serialize(split);
        var ordinaryJson = System.Text.Json.JsonSerializer.Serialize(ordinary);
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<Episode>(
            splitJson.Replace("\"AwaitingApproval\"", "\"Rejected\"", StringComparison.Ordinal));

        // Assert
        splitJson.Should().Contain("\"approvalStatus\":\"AwaitingApproval\"");
        Episode.CosmosAwaitingApprovalSql.Should().Contain("'AwaitingApproval'");
        ordinaryJson.Should().NotContain("approvalStatus");
        roundTripped!.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
    }

    [Fact(DisplayName =
        "An approved split-off that is later removed (e.g. unplayable) stays approved: it does not go back to " +
        "awaiting approval (#1484).")]
    public void approved_split_off_later_removed_stays_approved()
    {
        // Arrange
        var split = EpisodeServiceLinks.SplitOff(_fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast), [ServiceKeys.Spotify]).Episode;
        EpisodeServiceLinks.Approve(split);

        // Act
        split.Removed = true;

        // Assert
        split.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        split.IsAwaitingApproval().Should().BeFalse();
        split.IsHeldForApproval().Should().BeFalse();
        EpisodeServiceLinks.Approve(split).Should().BeFalse("approving an approved split-off changes nothing");
    }

    [Fact(DisplayName =
        "Approving a split-off again after it was approved, posted and then rejected makes it public but never " +
        "re-arms posting: approval never touches posting flags, so it keeps its posted state (#1484).")]
    public void re_approving_a_posted_split_off_does_not_re_arm_posting()
    {
        // Arrange
        var split = EpisodeServiceLinks.SplitOff(_fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast), [ServiceKeys.Spotify]).Episode;
        EpisodeServiceLinks.Approve(split);
        split.Posted = true;
        split.TweetPost = _fixture.CreateTitle(1);
        EpisodeServiceLinks.Reject(split);

        // Act
        EpisodeServiceLinks.Approve(split);

        // Assert
        split.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        split.Removed.Should().BeFalse();
        split.Posted.Should().BeTrue();
        split.Tweeted.Should().BeTrue();
    }

    [Fact(DisplayName = "Only a split-off episode can be approved or rejected (#1484).")]
    public void approving_or_rejecting_an_ordinary_episode_throws()
    {
        // Arrange
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);

        // Act
        var approve = () => EpisodeServiceLinks.Approve(episode);
        var reject = () => EpisodeServiceLinks.Reject(episode);

        // Assert
        approve.Should().Throw<InvalidOperationException>();
        reject.Should().Throw<InvalidOperationException>();
        episode.ApprovalStatus.Should().BeNull();
    }

    [Fact(DisplayName =
        "A split-off episode copies the source's search terms, guests, hashtag and subject matches, so the curator " +
        "starts from the same details (#1484).")]
    public void split_off_copies_search_terms_guests_hashtag_and_matches()
    {
        // Arrange
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        source.SearchTerms = _fixture.CreateTitle();
        source.HashTag = "#" + _fixture.CreateTitle(1).Replace(" ", string.Empty);
        source.Guests = [_fixture.CreateTitle(2), _fixture.CreateTitle(2)];
        source.Matches = [new PlayableSubjectMatch { Subject = _fixture.CreateTitle(1), Term = _fixture.CreateTitle(1) }];

        // Act
        var split = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;

        // Assert
        split.SearchTerms.Should().Be(source.SearchTerms);
        split.HashTag.Should().Be(source.HashTag);
        split.Guests.Should().Equal(source.Guests);
        split.Guests.Should().NotBeSameAs(source.Guests);
        split.Matches.Should().BeEquivalentTo(source.Matches);
        split.Matches[0].Should().NotBeSameAs(source.Matches[0]);
    }

    [Fact(DisplayName =
        "Splitting off services the source does not carry is refused, because it would create an episode with no links.")]
    public void split_off_without_links_is_refused()
    {
        // Arrange
        var source = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast);

        // Act
        var act = () => EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify, ServiceKeys.Apple]);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName =
        "Splitting the same links off the same stored episode twice gives the same split-off id (a name-based v5 " +
        "GUID), so an overwrite retried after a failed save replaces the saved split-off instead of adding another (#1482).")]
    public void split_off_id_is_deterministic()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var episodeId = _fixture.CreateGuid();
        Episode Source() => _fixture.BuildEpisode()
            .WithId(episodeId)
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();

        // Act
        var first = EpisodeServiceLinks.SplitOff(Source(), [ServiceKeys.Spotify]).Episode;
        var second = EpisodeServiceLinks.SplitOff(Source(), [ServiceKeys.Spotify]).Episode;

        // Assert
        first.Id.Should().Be(second.Id);
        first.Id.Version.Should().Be(5);
        first.Id.Should().NotBe(episodeId);
    }

    [Fact(DisplayName =
        "A different stored episode, or different links on it, gives a different split-off id, so the deterministic " +
        "id never reuses another episode's split-off (#1482).")]
    public void split_off_id_depends_on_episode_and_links()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var otherSpotifyId = _fixture.CreateSpotifyId();
        var episodeId = _fixture.CreateGuid();
        var source = _fixture.BuildEpisode().WithId(episodeId).WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId)).Create();
        var otherLinks = _fixture.BuildEpisode().WithId(episodeId).WithPodcast(_podcast)
            .WithSpotify(otherSpotifyId, _fixture.DefaultSpotifyUrl(otherSpotifyId)).Create();
        var otherEpisode = _fixture.BuildEpisode().WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId)).Create();

        // Act
        var id = EpisodeServiceLinks.SplitOffId(source);

        // Assert
        id.Should().NotBe(EpisodeServiceLinks.SplitOffId(otherLinks));
        id.Should().NotBe(EpisodeServiceLinks.SplitOffId(otherEpisode));
        EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode.Id.Should().Be(id);
    }

    [Fact(DisplayName =
        "The split-off id covers the stored episode's Spotify and Apple links whatever subset conflicts, so an " +
        "overwrite whose secondary Apple link resolves on one attempt and not the next still gets the same id (#1482).")]
    public void split_off_id_does_not_depend_on_the_conflicting_keys()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var episodeId = _fixture.CreateGuid();
        Episode Source() => _fixture.BuildEpisode()
            .WithId(episodeId)
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .Create();

        // Act
        var both = EpisodeServiceLinks.SplitOff(Source(), [ServiceKeys.Spotify, ServiceKeys.Apple]).Episode;
        var spotifyOnly = EpisodeServiceLinks.SplitOff(Source(), [ServiceKeys.Spotify]).Episode;

        // Assert
        spotifyOnly.Id.Should().Be(both.Id);
        EpisodeServiceLinks.SplitOffId(Source()).Should().Be(both.Id);
    }

    [Fact(DisplayName =
        "Finishing a split onto the existing split-off keeps its details, clears links it already holds from the " +
        "stored episode, and reports that it needs no save when it gained no link (#1482).")]
    public void split_off_onto_existing_keeps_its_details()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var episodeId = _fixture.CreateGuid();
        Episode Source() => _fixture.BuildEpisode()
            .WithId(episodeId)
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .Create();
        var existing = EpisodeServiceLinks.SplitOff(Source(), [ServiceKeys.Spotify, ServiceKeys.Apple]).Episode;
        var editedTitle = _fixture.CreateTitle();
        existing.Title = editedTitle;
        var source = Source();

        // Act
        var result = EpisodeServiceLinks.SplitOffOnto(source, existing, [ServiceKeys.Spotify]);

        // Assert
        result.Episode.Should().BeSameAs(existing);
        result.Reused.Should().BeTrue();
        result.SplitOffChanged.Should().BeFalse();
        result.Moved.Should().BeEquivalentTo([ServiceKeys.Spotify, ServiceKeys.Apple]);
        existing.Title.Should().Be(editedTitle);
        EpisodeServicePresence.SpotifyEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.AppleEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.SpotifyEpisodeId(existing).Should().Be(spotifyId);
        EpisodeServicePresence.AppleEpisodeId(existing).Should().Be(appleId);
    }

    [Fact(DisplayName =
        "Finishing a split onto an existing split-off that lacks a conflicting link moves that link onto it and " +
        "reports that it must be saved (#1482).")]
    public void split_off_onto_existing_adds_a_missing_link()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var episodeId = _fixture.CreateGuid();
        Episode Source() => _fixture.BuildEpisode()
            .WithId(episodeId)
            .WithPodcast(_podcast)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .Create();
        var existing = EpisodeServiceLinks.SplitOff(Source(), [ServiceKeys.Spotify]).Episode;
        var source = Source();

        // Act
        var result = EpisodeServiceLinks.SplitOffOnto(source, existing, [ServiceKeys.Spotify, ServiceKeys.Apple]);

        // Assert
        result.SplitOffChanged.Should().BeTrue();
        EpisodeServicePresence.AppleEpisodeId(existing).Should().Be(appleId);
        EpisodeServicePresence.AppleEpisodeId(source).Should().BeNull();
        EpisodeServicePresence.SpotifyEpisodeId(source).Should().BeNull();
    }
}
