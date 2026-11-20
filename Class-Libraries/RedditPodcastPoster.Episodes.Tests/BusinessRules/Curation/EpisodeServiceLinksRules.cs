using FluentAssertions;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

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
        "A split-off episode is marked as already posted to Reddit, X and Bluesky, because it is a recovery record " +
        "and must never be posted automatically.")]
    public void split_off_episode_is_never_posted()
    {
        // Arrange
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        source.Posted = false;

        // Act
        var split = EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode;

        // Assert
        split.Posted.Should().BeTrue();
        split.Tweeted.Should().BeTrue();
        split.BlueskyPosted.Should().BeTrue();
        split.TweetPost.Should().BeNull("the source's post ids must not be shared, or un-posting one would delete the other's post");
        split.BlueskyPost.Should().BeNull();
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
        var id = EpisodeServiceLinks.SplitOffId(source, [ServiceKeys.Spotify]);

        // Assert
        id.Should().NotBe(EpisodeServiceLinks.SplitOffId(otherLinks, [ServiceKeys.Spotify]));
        id.Should().NotBe(EpisodeServiceLinks.SplitOffId(otherEpisode, [ServiceKeys.Spotify]));
        EpisodeServiceLinks.SplitOff(source, [ServiceKeys.Spotify]).Episode.Id.Should().Be(id);
    }
}
