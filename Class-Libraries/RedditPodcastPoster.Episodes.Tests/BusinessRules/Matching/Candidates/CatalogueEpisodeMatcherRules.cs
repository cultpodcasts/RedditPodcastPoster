using FluentAssertions;
using RedditPodcastPoster.EpisodeLookup;
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
        "Incident (Aug 2026): when enriching a YouTube-discovered episode, a sole same-day row with in-band but not " +
        "near-exact duration, a divergent title, and no shared subjects is not selected.")]
    public void Youtube_enrichment_rejects_in_band_not_near_exact_duration_without_title_confidence()
    {
        // Arrange — ~2m gap stays in the five-minute band but outside near-exact tolerance
        var probeLength = TimeSpan.FromMinutes(44);
        var candidateLength = probeLength + TimeSpan.FromMinutes(2);
        var lookup = Lookup(YouTubeTitle, DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(10)), probeLength, Service.Spotify, enriching: true);
        var candidate = Audio(DisjointAudioTitle, DomainTestFixture.UtcDateDaysAgo(2), candidateLength);

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode, a sole same-day row with near-exact duration is selected " +
        "even with a divergent marketing title and no shared subjects.")]
    public void Youtube_enrichment_accepts_near_exact_duration_same_day_disjoint_marketing_titles()
    {
        // Arrange
        var length = TimeSpan.FromSeconds(4149);
        var probeRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(15));
        var lookup = Lookup(
            "How Brands Are Brainwashing You About Consumer Habits And Influence",
            probeRelease,
            length,
            Service.Spotify,
            enriching: true);
        var candidate = Audio(
            "Hoodwinked: A Conversation About Marketing Influence And Persuasion Tactics",
            DomainTestFixture.SameCalendarDateWithTime(probeRelease, TimeSpan.FromHours(9)),
            length);

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeSameAs(candidate);
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
        var day = DomainTestFixture.UtcDateDaysAgo(1);
        var lookup = Lookup(
            "Why Is Everyone Lying to You for Money? The Great Crypto Scam EXPOSED (w/ Ben McKenzie)",
            day.Add(new TimeSpan(8, 0, 39)),
            new TimeSpan(0, 56, 49),
            Service.YouTube,
            enriching: true);
        var probe = Probe(lookup);
        probe.Subjects = ["Cult Psychology", "Multi-Level Marketing"];
        var audio = Audio(
            "Everyone Is Lying to You for Money, Crypto and Cult Psychology with Ben McKenzie: My conversation with the author of Easy Money about financial manipulation, the need to belong, and protecting our freedom of mind.",
            day.AddHours(9),
            new TimeSpan(0, 59, 29));
        audio.Subjects = ["Cult Psychology", "Multi-Level Marketing"];
        var previousWeek = Audio("Volcanic Origami Workshop", day.AddDays(-7).AddHours(9), new TimeSpan(0, 58, 10));

        // Act
        var result = _sut.Match(lookup, probe, [previousWeek, audio], null);

        // Assert
        result.Should().BeSameAs(audio);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode whose publishing delay moves the expected audio release onto the " +
        "previous day, and the lookup carries the unshifted YouTube publish, Match selects the near-exact-length audio " +
        "published the same day as the video despite a disjoint title.")]
    public void Youtube_enrichment_accepts_audio_on_raw_youtube_publish_day_when_delay_crosses_midnight()
    {
        // Arrange
        var delay = TimeSpan.FromHours(7);
        var youTubePublished = DomainTestFixture.UtcAtTime(-3, new TimeSpan(4, 0, 15));
        var length = new TimeSpan(1, 2, 14);
        var lookup = Lookup(YouTubeTitle, youTubePublished - delay, length, Service.YouTube, enriching: true) with
        {
            YouTubePublishingDelay = delay,
            YouTubePublished = youTubePublished
        };
        var candidate = Audio(DisjointAudioTitle, DomainTestFixture.UtcDateDaysAgo(3), length + TimeSpan.FromSeconds(12));

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeSameAs(candidate);
    }

    [Fact(DisplayName =
        "When the same delay-shifted lookup does not carry the unshifted YouTube publish, Match does not select the " +
        "next-day audio, because near-exact length plus a weak one-day release window stays below the threshold.")]
    public void Youtube_enrichment_without_raw_publish_keeps_weak_window_below_threshold()
    {
        // Arrange
        var delay = TimeSpan.FromHours(7);
        var youTubePublished = DomainTestFixture.UtcAtTime(-3, new TimeSpan(4, 0, 15));
        var length = new TimeSpan(1, 2, 14);
        var lookup = Lookup(YouTubeTitle, youTubePublished - delay, length, Service.YouTube, enriching: true) with
        {
            YouTubePublishingDelay = delay
        };
        var candidate = Audio(DisjointAudioTitle, DomainTestFixture.UtcDateDaysAgo(3), length + TimeSpan.FromSeconds(12));

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [candidate], null);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a YouTube-only video published the day after a full episode is enriched, Match does not take the previous " +
        "day's near-exact-length audio, because the raw publish day differs from the audio day.")]
    public void Youtube_enrichment_next_day_video_does_not_take_previous_day_audio()
    {
        // Arrange
        var delay = TimeSpan.FromHours(7);
        var youTubePublished = DomainTestFixture.UtcAtTime(-2, new TimeSpan(19, 1, 32));
        var length = new TimeSpan(1, 1, 11);
        var lookup = Lookup(YouTubeTitle, youTubePublished - delay, length, Service.YouTube, enriching: true) with
        {
            YouTubePublishingDelay = delay,
            YouTubePublished = youTubePublished
        };
        var previousDayAudio = Audio(DisjointAudioTitle, DomainTestFixture.UtcDateDaysAgo(3), length - TimeSpan.FromSeconds(14));

        // Act
        var result = _sut.Match(lookup, Probe(lookup), [previousDayAudio], null);

        // Assert
        result.Should().BeNull();
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
