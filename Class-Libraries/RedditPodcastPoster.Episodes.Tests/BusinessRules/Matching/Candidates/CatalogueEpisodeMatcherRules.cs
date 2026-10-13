using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching.Candidates;

/// <summary>
/// The shared catalogue matching step used by every audio platform (ported from the Spotify finder wrapper rules):
/// release-band slicing, strategy choice and the YouTube-enrichment guards.
/// </summary>
public class CatalogueEpisodeMatcherRules
{
    private const string YouTubeTitle = "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
    private const string DisjointAudioTitle = "A Decade Inside An Arranged Marriage And The Exit That Followed";
    private const string SharedTitle = "Lantern Harbour Night Stories With A Guest From The Archive";
    private readonly DomainTestFixture _fixture = new();
    private readonly CatalogueEpisodeMatcher _sut = new(EpisodeDomainTestServices.CreatePlatformMatcher());

    [Fact(DisplayName =
        "When a reducer excludes an already-assigned candidate, Match returns the other same-title, same-length candidate.")]
    public void Reducer_excludes_assigned_candidates()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(2);
        var lookup = Lookup(SharedTitle, release, TimeSpan.FromMinutes(44), Service.YouTube);
        var assigned = Audio(SharedTitle, release, TimeSpan.FromMinutes(44));
        var available = Audio(SharedTitle, release, TimeSpan.FromMinutes(44));

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [assigned, available], null, e => !ReferenceEquals(e, assigned));

        // Assert
        result.Should().BeSameAs(available);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode, a sole row within five minutes of duration but outside the release " +
        "window and with a disjoint title is not selected (wrong-week duration snipe).")]
    public void Youtube_enrichment_does_not_duration_snipe_disjoint_title()
    {
        // Arrange
        var length = TimeSpan.FromMinutes(44);
        var lookup = Lookup(YouTubeTitle, DomainTestFixture.UtcAtTime(-9, new TimeSpan(3, 30, 46)), length, Service.YouTube, enriching: true);
        var candidate = Audio(DisjointAudioTitle, DomainTestFixture.UtcDateDaysAgo(7), length + TimeSpan.FromSeconds(179));

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "Incident (Aug 2026): when enriching a YouTube-discovered episode, a sole same-day row with the same duration but a " +
        "divergent title and no shared subjects is not selected.")]
    public void Youtube_enrichment_rejects_unique_duration_without_title_confidence()
    {
        // Arrange
        var length = TimeSpan.FromMinutes(44);
        var lookup = Lookup(YouTubeTitle, DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(10)), length, Service.Spotify, enriching: true);
        var candidate = Audio(DisjointAudioTitle, DomainTestFixture.UtcDateDaysAgo(2), length);

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode, a same-day row whose title contains the YouTube title is selected.")]
    public void Youtube_enrichment_accepts_title_containment()
    {
        // Arrange
        var length = TimeSpan.FromMinutes(44);
        var probeRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(17) + TimeSpan.FromMinutes(28));
        var lookup = Lookup(SharedTitle, probeRelease, length, Service.Spotify, enriching: true);
        var candidate = Audio($"{SharedTitle}: editorial rename and the exit that followed", probeRelease.Date, length);

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeSameAs(candidate);
    }

    [Fact(DisplayName =
        "Duration-aware matching never accepts a unique duration without title confidence.")]
    public void Never_accepts_unique_duration_without_title_match()
    {
        // Arrange
        var lookup = Lookup("aaaaaaaa", DomainTestFixture.UtcDateDaysAgo(2), TimeSpan.FromMinutes(45), Service.YouTube);
        var candidate = Audio("zzzzzzzz", DomainTestFixture.UtcDateDaysAgo(2), TimeSpan.FromMinutes(45));

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When neither YouTube authority nor YouTube enrichment applies, Match uses release-date matching, so an " +
        "audio-to-audio lookup with a length still matches a same-day same-title row whose duration differs.")]
    public void Audio_lookup_uses_release_date_strategy()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(3);
        var lookup = Lookup(SharedTitle, release, TimeSpan.FromMinutes(44), Service.Spotify);
        var candidate = Audio(SharedTitle + " (Part 1)", release, TimeSpan.FromMinutes(61));

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        lookup.UsesLengthStrategy.Should().BeFalse();
        result.Should().BeSameAs(candidate);
    }

    [Fact(DisplayName =
        "When a release date is known, an in-band candidate wins over a years-apart candidate with the same title, " +
        "and the years-apart one is only tried after the band misses.")]
    public void In_band_slice_is_matched_before_out_of_band()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(60);
        var lookup = Lookup(SharedTitle, release, null, Service.Spotify);
        var yearsApart = Audio(SharedTitle, release.AddYears(-3), TimeSpan.FromMinutes(44));
        var inBand = Audio(SharedTitle, release, TimeSpan.FromMinutes(44));
        var inBandOther = Audio("Volcanic Origami Workshop", release, TimeSpan.FromMinutes(44));

        // Act
        var first = _sut.Match(lookup, Probe(lookup), [yearsApart, inBand], null);
        var fallback = _sut.Match(lookup, Probe(lookup), [yearsApart, inBandOther], null);

        // Assert
        first.Should().BeSameAs(inBand);
        fallback.Should().BeSameAs(yearsApart);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode whose YouTube title and cut differ from the audio feed " +
        "(Steve Hassan / Ben McKenzie, 12 Oct 2026: 56:49 YouTube vs 59:29 audio, same day) and both share classified subjects, " +
        "Match selects the audio episode.")]
    public void Real_divergent_pair_matches_with_shared_subjects()
    {
        // Arrange
        var lookup = Lookup(
            "Why Is Everyone Lying to You for Money? The Great Crypto Scam EXPOSED (w/ Ben McKenzie)",
            new DateTime(2026, 10, 12, 8, 0, 39, DateTimeKind.Utc),
            new TimeSpan(0, 56, 49),
            Service.YouTube,
            enriching: true);
        var probe = Probe(lookup);
        probe.Subjects = ["Cult Psychology", "Multi-Level Marketing"];
        var audio = Audio(
            "Everyone Is Lying to You for Money, Crypto and Cult Psychology with Ben McKenzie: My conversation with the author of Easy Money about financial manipulation, the need to belong, and protecting our freedom of mind.",
            new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
            new TimeSpan(0, 59, 29));
        audio.Subjects = ["Cult Psychology", "Multi-Level Marketing"];
        var previousWeek = Audio("Volcanic Origami Workshop", new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc), new TimeSpan(0, 58, 10));

        // Act
        var result = _sut.Match(lookup, probe, [previousWeek, audio], null);

        // Assert
        result.Should().BeSameAs(audio);
    }

    private static CatalogueLookup Lookup(string title, DateTime? released, TimeSpan? length, Service? authority, bool enriching = false) =>
        new("Podcast", title, released, length, authority, enriching);

    private static Episode Probe(CatalogueLookup lookup) => new()
    {
        Title = lookup.Title,
        Description = string.Empty,
        Length = lookup.Length ?? TimeSpan.Zero,
        ReleaseUtc = lookup.Released ?? DateTime.MinValue
    };

    private Episode Audio(string title, DateTime release, TimeSpan length)
    {
        var episode = new Episode { Title = title, Description = string.Empty, Length = length, ReleaseUtc = release };
        EpisodeServicePresence.SetSpotifyIdentity(episode, _fixture.CreateSpotifyId());
        return episode;
    }
}
