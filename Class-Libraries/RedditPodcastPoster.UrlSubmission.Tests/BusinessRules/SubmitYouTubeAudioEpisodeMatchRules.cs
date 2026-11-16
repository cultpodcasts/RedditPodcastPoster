using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Matchers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules;

/// <summary>
/// #1445: submitting a Spotify URL to an existing podcast must enrich the stored YouTube episode
/// when the two platforms carry a lightly edited title but agree on release (16 seconds apart)
/// and duration (5621s vs 5620s).
/// </summary>
public class SubmitYouTubeAudioEpisodeMatchRules
{
    // Regression baseline: the exact platform titles from #1445. The rule depends on how these two
    // strings score, so they are kept verbatim rather than generated.
    private const string YouTubeTitle =
        "Celebrity exorcist who gave UNQUALIFIED \"medical advice\" SILENCED by bishop for ONE YEAR (pt.II)";

    private const string AudioTitle =
        "Celebrity exorcist who gave BAD medical \"advice\" PUBLICLY SILENCED by bishop for ONE YEAR (pt.II)";

    private static readonly TimeSpan YouTubeLength = TimeSpan.FromSeconds(5621);
    private static readonly TimeSpan AudioLength = TimeSpan.FromSeconds(5620);
    private static readonly TimeSpan YouTubeLagBehindAudio = TimeSpan.FromSeconds(16);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly DateTime _audioRelease;

    public SubmitYouTubeAudioEpisodeMatchRules()
    {
        _podcast = _fixture.CreatePodcast();
        _audioRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(15));
    }

    private IEpisodeHelper Sut => _mocker.CreateInstance<EpisodeHelper>();

    [Fact(DisplayName =
        "When a Spotify URL is submitted and the stored YouTube-only episode has a lightly edited title, " +
        "a release 16 seconds later and the same duration, it matches, because release and duration " +
        "corroborate a title that scores below the title-only bar.")]
    public void spotify_submit_matches_youtube_episode_with_edited_title_same_release_and_duration()
    {
        // Arrange
        var stored = StoredYouTubeEpisode(_audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength);
        var item = SpotifySubmit(AudioTitle, _audioRelease, AudioLength);
        var titleScore = FuzzyMatcher.Score(AudioTitle, YouTubeTitle);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        titleScore.Should().BeInRange(EpisodeHelper.MinCrossPlatformFuzzyTitleMatch, 94,
            "the edited titles must sit between the cross-platform bar and the title-only bar of 95");
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the Spotify release is date-only (midnight UTC) on the YouTube publish day, the edited-title pair " +
        "still matches, because the Spotify catalogue carries no time of day and the same calendar day is enough.")]
    public void spotify_submit_with_date_only_release_matches_youtube_episode_on_same_day()
    {
        // Arrange
        var stored = StoredYouTubeEpisode(_audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength);
        var item = SpotifySubmit(AudioTitle, _audioRelease.Date, AudioLength);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a YouTube URL is submitted and the stored Spotify-only episode has the lightly edited title " +
        "on the same release and duration, it matches, because the YouTube↔audio rule is symmetric.")]
    public void youtube_submit_matches_spotify_episode_with_edited_title_same_release_and_duration()
    {
        // Arrange
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast, _audioRelease, AudioLength, AudioTitle);
        var youTubeId = _fixture.CreateYouTubeId();
        var youTube = new CategorisedYouTubeItem(
            _fixture.CreateYouTubeChannelId(), youTubeId, _podcast.Name, string.Empty, string.Empty,
            YouTubeTitle, string.Empty, _audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength,
            _fixture.DefaultYouTubeUrl(youTubeId), false, null, null);
        var item = new CategorisedItem(_podcast, [stored], null, null, null, youTube, null, Service.YouTube);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the edited titles share a calendar day but the durations differ by more than the cross-platform " +
        "band, the stored YouTube episode does not match, because a similar title alone is not enough.")]
    public void spotify_submit_does_not_match_when_duration_is_outside_cross_platform_band()
    {
        // Arrange
        var stored = StoredYouTubeEpisode(_audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength);
        var item = SpotifySubmit(
            AudioTitle,
            _audioRelease,
            YouTubeLength - EpisodeHelper.CrossPlatformDurationTolerance(YouTubeLength, YouTubeLength) -
            TimeSpan.FromSeconds(1));

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the edited titles and duration agree but the Spotify release is days away from the YouTube publish, " +
        "the stored episode does not match, because a recycled title on another day must not attach.")]
    public void spotify_submit_does_not_match_when_release_is_on_a_different_day()
    {
        // Arrange
        var stored = StoredYouTubeEpisode(_audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength);
        var item = SpotifySubmit(AudioTitle, _audioRelease.AddDays(3), AudioLength);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When release and duration agree but the stored title is unrelated, the stored episode does not match, " +
        "because release and duration alone do not identify an episode on submit.")]
    public void spotify_submit_does_not_match_unrelated_title_on_same_release_and_duration()
    {
        // Arrange
        var unrelatedTitle = _fixture.CreateTitleNotContainedIn(AudioTitle);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            _podcast, _audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength, unrelatedTitle);
        var item = SpotifySubmit(AudioTitle, _audioRelease, AudioLength);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        FuzzyMatcher.Score(AudioTitle, unrelatedTitle).Should().BeLessThan(EpisodeHelper.MinCrossPlatformFuzzyTitleMatch);
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the stored episode already carries a different Spotify id, the edited-title pair does not match, " +
        "because a conflicting identity is not silently overwritten on submit.")]
    public void spotify_submit_does_not_match_episode_with_conflicting_spotify_id()
    {
        // Arrange
        var stored = StoredYouTubeEpisode(_audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength);
        EpisodeServicePresence.SetSpotifyIdentity(stored, _fixture.CreateSpotifyId());
        var item = SpotifySubmit(AudioTitle, _audioRelease, AudioLength);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a secondary Apple id resolved for a Spotify submit is already on a different stored episode " +
        "with an unrelated title, an earlier release and a different duration, that episode does not match, " +
        "because a secondary id hit must still look like the submitted episode.")]
    public void secondary_apple_id_on_unrelated_episode_does_not_match()
    {
        // Arrange
        var appleId = _fixture.CreateAppleId();
        var unrelatedTitle = _fixture.CreateTitleNotContainedIn(AudioTitle);
        var wrong = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithTitle(unrelatedTitle)
            .WithRelease(_audioRelease.AddHours(-37))
            .WithLength(AudioLength + TimeSpan.FromMinutes(13))
            .WithYouTube(_fixture.CreateYouTubeId())
            .WithSpotify(_fixture.CreateSpotifyId())
            .WithApple(appleId)
            .Create();
        var item = SpotifySubmit(AudioTitle, _audioRelease, AudioLength) with
        {
            ResolvedAppleItem = AppleItem(appleId)
        };

        // Act
        var result = Sut.IsMatchingEpisode(wrong, item);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a secondary Apple id resolved for a Spotify submit is on the stored episode that matches the " +
        "submitted release, duration and edited title, it still matches, because the sanity check only " +
        "rejects implausible hits.")]
    public void secondary_apple_id_on_matching_episode_still_matches()
    {
        // Arrange
        var appleId = _fixture.CreateAppleId();
        var stored = StoredYouTubeEpisode(_audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength);
        EpisodeServicePresence.SetAppleIdentity(stored, appleId);
        var item = SpotifySubmit(AudioTitle, _audioRelease, AudioLength) with
        {
            ResolvedAppleItem = AppleItem(appleId)
        };

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the durations differ by just under the YouTube↔audio band (the greater of 2 minutes or 3%), " +
        "the edited-title pair still matches, because the band is inclusive of near-identical lengths.")]
    public void spotify_submit_matches_when_duration_is_just_inside_cross_platform_band()
    {
        // Arrange
        var band = EpisodeHelper.CrossPlatformDurationTolerance(YouTubeLength, YouTubeLength);
        var stored = StoredYouTubeEpisode(_audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength);
        var item = SpotifySubmit(AudioTitle, _audioRelease, YouTubeLength - band + TimeSpan.FromSeconds(1));

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        band.Should().BeLessThan(TimeSpan.FromMinutes(5), "the band is tighter than the generic cross-platform band");
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the stored YouTube episode is part I and the submitted Spotify episode is part II of the same " +
        "story on the same day with a similar duration, it does not match, because differing part numbers " +
        "mark different episodes however close the titles score.")]
    public void spotify_submit_part_two_does_not_match_youtube_part_one_on_same_day()
    {
        // Arrange
        var partOneTitle = YouTubeTitle.Replace("(pt.II)", "(pt.I)");
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            _podcast, _audioRelease.Add(YouTubeLagBehindAudio), YouTubeLength - TimeSpan.FromSeconds(60),
            partOneTitle);
        var item = SpotifySubmit(AudioTitle, _audioRelease, AudioLength);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        FuzzyMatcher.Score(AudioTitle, partOneTitle)
            .Should().BeGreaterThanOrEqualTo(EpisodeHelper.MinCrossPlatformFuzzyTitleMatch,
                "the part marker, not the fuzzy score, is what must block this pair");
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a submitted title differs from a stored title only by its part number, the title-only path " +
        "does not match, because part 1 and part 2 of a story are different episodes even at a 95+ score.")]
    public void title_only_path_does_not_match_differing_part_numbers()
    {
        // Arrange
        var stem = _fixture.CreateLongTitle();
        var storedTitle = stem + " (part 1)";
        var submittedTitle = stem + " (part 2)";
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            _podcast, _audioRelease.AddDays(-10), YouTubeLength, storedTitle);
        var item = SpotifySubmit(submittedTitle, _audioRelease, AudioLength);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        FuzzyMatcher.Score(submittedTitle, storedTitle).Should().BeGreaterThanOrEqualTo(95,
            "the titles must clear the title-only bar so the part guard is what blocks them");
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a stored title is contained in the submitted title but the part numbers differ (pt 1 vs pt 10), " +
        "the stored episode does not match, because containment must not cross part numbers.")]
    public void contains_path_does_not_match_differing_part_numbers()
    {
        // Arrange
        var stem = _fixture.CreateTitle();
        var storedTitle = stem + " pt 1";
        var submittedTitle = stem + " pt 10";
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            _podcast, _audioRelease.AddDays(-10), YouTubeLength, storedTitle);
        var item = SpotifySubmit(submittedTitle, _audioRelease, AudioLength);

        // Act
        var result = Sut.IsMatchingEpisode(stored, item);

        // Assert
        submittedTitle.Should().Contain(storedTitle);
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a secondary Apple id resolved for a Spotify submit is on a stored episode with an unrelated title " +
        "but the same day and the same duration, that episode does not match, because a secondary id hit " +
        "needs title evidence and release and duration alone are not enough.")]
    public void secondary_apple_id_on_unrelated_title_same_day_same_duration_does_not_match()
    {
        // Arrange
        var appleId = _fixture.CreateAppleId();
        var unrelatedTitle = _fixture.CreateTitleNotContainedIn(AudioTitle);
        var sibling = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            _podcast, _audioRelease.Add(YouTubeLagBehindAudio), AudioLength, unrelatedTitle);
        EpisodeServicePresence.SetAppleIdentity(sibling, appleId);
        var item = SpotifySubmit(AudioTitle, _audioRelease, AudioLength) with
        {
            ResolvedAppleItem = AppleItem(appleId)
        };

        // Act
        var result = Sut.IsMatchingEpisode(sibling, item);

        // Assert
        FuzzyMatcher.Score(AudioTitle, unrelatedTitle).Should().BeLessThan(EpisodeHelper.MinCrossPlatformFuzzyTitleMatch);
        result.Should().BeFalse();
    }

    private Episode StoredYouTubeEpisode(DateTime release, TimeSpan length) =>
        _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast, release, length, YouTubeTitle);

    private CategorisedItem SpotifySubmit(string title, DateTime release, TimeSpan duration)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var spotify = new CategorisedSpotifyItem(
            _fixture.CreateSpotifyId(), spotifyId, _podcast.Name, string.Empty, string.Empty, title,
            string.Empty, release, duration, _fixture.DefaultSpotifyUrl(spotifyId), false, null);
        return new CategorisedItem(_podcast, [], null, spotify, null, null, null, Service.Spotify);
    }

    private CategorisedAppleItem AppleItem(long appleId) =>
        new(_fixture.CreateAppleId(), appleId, _podcast.Name, string.Empty, string.Empty, AudioTitle,
            string.Empty, _audioRelease, AudioLength, _fixture.DefaultAppleUrl(appleId), false, null);
}
