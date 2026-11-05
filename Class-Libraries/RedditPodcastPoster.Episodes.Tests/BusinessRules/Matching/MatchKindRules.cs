using FluentAssertions;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// <see cref="IEpisodePlatformMatcher.Classify"/>: certain paths (platform id, exact title with
/// duration + release-strategy alignment, EpisodeMatchRegex) are never scored by the merge;
/// heuristic paths (fuzzy title + duration + release alignment, release + duration) may be.
/// </summary>
public class MatchKindRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly IEpisodePlatformMatcher _matcher = EpisodeDomainTestServices.CreatePlatformMatcher();
    private readonly Podcast _podcast;
    private readonly DateTime _alignedRelease;
    private readonly string _youTubeId;
    private readonly string _otherYouTubeId;

    public MatchKindRules()
    {
        _podcast = _fixture.CreatePodcast();
        _alignedRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        _youTubeId = _fixture.CreateYouTubeId();
        _otherYouTubeId = _fixture.CreateYouTubeId();
    }

    private Episode YouTube(string id, string title, TimeSpan length, DateTime? release = null) =>
        _fixture.CreateStoredEpisode(_podcast, e =>
        {
            e.Title = title;
            e.Length = length;
            e.ReleaseUtc = release ?? _alignedRelease;
            EpisodeServicePresence.SetYouTubeIdentity(e, id);
        });

    private Episode Spotify(string title, TimeSpan length, DateTime release) =>
        _fixture.CreateStoredEpisode(_podcast, e =>
        {
            e.Title = title;
            e.Length = length;
            e.ReleaseUtc = release;
            EpisodeServicePresence.SetSpotifyIdentity(e, _fixture.CreateSpotifyId());
        });

    private EpisodeMatchKind Classify(Episode existing, Episode incoming, Regex? regex = null) =>
        _matcher.Classify(existing, incoming, regex, _podcast, [existing]);

    [Fact(DisplayName = "The same YouTube id is a certain match.")]
    public void Same_youtube_id_is_certain()
    {
        // Arrange
        var existing = YouTube(_youTubeId, _fixture.CreateTitle(), _fixture.CreateDuration());
        var incoming = YouTube(_youTubeId, _fixture.CreateTitle(), _fixture.CreateDuration());

        // Act
        var kind = Classify(existing, incoming);

        // Assert
        kind.Should().Be(EpisodeMatchKind.Certain);
    }

    [Fact(DisplayName = "Different YouTube ids never match.")]
    public void Different_youtube_ids_are_none()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var length = _fixture.CreateDuration();

        // Act
        var kind = Classify(YouTube(_youTubeId, title, length), YouTube(_otherYouTubeId, title, length));

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName =
        "An exact title with matching duration and the same calendar day is a certain match.")]
    public void Exact_title_with_day_and_duration_is_certain()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var length = _fixture.CreateDuration();

        // Act
        var kind = Classify(YouTube(_youTubeId, title, length), Spotify(title, length, _alignedRelease));

        // Assert
        kind.Should().Be(EpisodeMatchKind.Certain);
    }

    [Fact(DisplayName =
        "An exact title alone is not a certain match when release or duration clearly differ, " +
        "because title must not override release-strategy and duration gates.")]
    public void Exact_title_alone_is_not_certain()
    {
        // Arrange
        var title = _fixture.CreateTitle();

        // Act
        var kind = Classify(
            YouTube(_youTubeId, title, TimeSpan.FromHours(1)),
            Spotify(title, TimeSpan.FromHours(3), _alignedRelease.AddYears(-2)));

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName =
        "An exact title with matching duration but a misaligned release is not a match, " +
        "because title and duration must not override the release-strategy gate.")]
    public void Exact_title_matching_duration_misaligned_release_is_none()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var length = _fixture.CreateDuration();

        // Act
        var kind = Classify(
            YouTube(_youTubeId, title, length),
            Spotify(title, length, _alignedRelease.AddDays(-30)));

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName =
        "A near-exact or case-variant title with matching duration but without release alignment " +
        "must not Heuristic-merge on YouTube↔audio, because recycled titles are not the same episode.")]
    public void Near_exact_title_matching_duration_misaligned_release_is_none()
    {
        // Arrange — case variant skips byte-exact Certain and would otherwise Fuzzy+duration Heuristic-merge.
        var title = _fixture.CreateShortTitle();
        var caseVariant = title.ToUpperInvariant();
        var length = _fixture.CreateDuration();

        // Act
        var kind = Classify(
            YouTube(_youTubeId, title, length),
            Spotify(caseVariant, length, _alignedRelease.AddDays(-30)));

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName = "An EpisodeMatchRegex episode number match is certain.")]
    public void Episode_match_regex_is_certain()
    {
        // Arrange
        var existing = YouTube(_youTubeId, "Episode 7 - old", TimeSpan.FromHours(1));
        var incoming = Spotify("Episode 7 - new", TimeSpan.FromHours(3), _alignedRelease.AddYears(-2));
        var regex = new Regex(@"Episode (?'episodematch'\d+)", Podcast.EpisodeMatchFlags);

        // Act
        var kind = Classify(existing, incoming, regex);

        // Assert
        kind.Should().Be(EpisodeMatchKind.Certain);
    }

    [Fact(DisplayName =
        "A fuzzy title with a duration within tolerance and release alignment is a heuristic match.")]
    public void Fuzzy_title_and_duration_is_heuristic()
    {
        // Arrange — word-reordered titles share duration and calendar day.
        const string youTubeTitle = "TikTok Live chat about Sister Wives";
        const string spotifyTitle = "Sister Wives New Season TikTok Live chat about";
        var length = TimeSpan.FromMinutes(95);

        // Act
        var kind = Classify(
            YouTube(_youTubeId, youTubeTitle, length),
            Spotify(spotifyTitle, length, _alignedRelease));

        // Assert
        kind.Should().Be(EpisodeMatchKind.Heuristic);
    }
}
