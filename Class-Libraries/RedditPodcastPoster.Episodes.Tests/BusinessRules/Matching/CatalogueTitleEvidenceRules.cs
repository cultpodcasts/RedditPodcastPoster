using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// #1445 indexer regression: a YouTube-discovered multi-part episode took the next part's Spotify and Apple audio
/// on shared show boilerplate, a shared subject and a next-day release, with no title or length agreement.
/// </summary>
public class CatalogueTitleEvidenceRules
{
    // Real-world shape: part one runs 1:47:25, part two 1:33:41 (14 minutes apart, outside the band).
    private static readonly TimeSpan PartOneLength = TimeSpan.FromSeconds(6445);
    private static readonly TimeSpan PartTwoLength = TimeSpan.FromSeconds(5620);

    private const string PartOneTitle =
        "Travelling preacher who hid a decade of complaints exposed by former followers (pt. one)";
    private const string PartTwoAudioTitle =
        "Travelling preacher who gave BAD spiritual \"counsel\" quietly removed by his order for a year (pt.II)";
    private const string PartTwoVideoTitle =
        "Travelling preacher who gave UNQUALIFIED \"counsel\" removed by his order for a year (pt.II)";
    private const string SharedSubject = "Religious Orders";

    private const string ShowBoilerplate =
        "Consider supporting the channel through the Harbour Trust membership page.\n" +
        "Northgate Abbey Survivors Network offers confidential help to anyone affected.\n" +
        "New episodes every week, so subscribe and share with anyone who needs to hear it.";

    private readonly DomainTestFixture _fixture = new();
    private readonly IEpisodePlatformMatcher _matcher = EpisodeDomainTestServices.CreatePlatformMatcher();

    [Fact(DisplayName =
        "When a YouTube part-one probe and next-day part-two Spotify audio share only show boilerplate, a subject " +
        "and the release window, with lengths 14 minutes apart, the score is zero, because conflicting part numbers " +
        "are a hard reject and without title or in-band duration evidence nothing else may carry the match.")]
    public void part_one_probe_against_next_day_part_two_audio_scores_zero()
    {
        // Arrange
        var probe = CreatePartOneProbe();
        var partTwoAudio = CreatePartTwoAudio();

        // Act
        var score = CatalogueMatchScorer.Score(probe, partTwoAudio);
        var matched = _matcher.FindCatalogueMatchByLength(
            probe,
            [partTwoAudio],
            _fixture.CreatePodcast(),
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        EpisodeTitlePartMarkers.Conflict(probe.Title, partTwoAudio.Title).Should().BeTrue();
        score.Should().Be(0);
        matched.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the part markers are taken off the same pair, description, proper nouns, subject and release still " +
        "score zero, because a candidate with no title match and an out-of-band duration needs title evidence.")]
    public void same_pair_without_part_markers_scores_zero_without_title_evidence()
    {
        // Arrange
        var probe = CreatePartOneProbe(title: "Travelling preacher who hid a decade of complaints exposed by followers");
        var audio = CreatePartTwoAudio(title: "Order quietly removes a preacher for a year after bad spiritual counsel");

        // Act
        var score = CatalogueMatchScorer.Score(probe, audio);

        // Assert
        EpisodeTitlePartMarkers.Conflict(probe.Title, audio.Title).Should().BeFalse();
        (probe.Length - audio.Length).Should().BeGreaterThan(CatalogueMatchScorer.GetDurationBand(probe.Length, audio.Length));
        score.Should().Be(0);
    }

    [Fact(DisplayName =
        "When the YouTube part-two probe and its same-day Spotify audio have an edited title and lengths a second " +
        "apart, the score meets the threshold and the matcher picks it over the part-one audio, because title " +
        "evidence and near-exact duration are present and part numbers agree.")]
    public void part_two_probe_matches_its_own_audio_and_not_part_one_audio()
    {
        // Arrange
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = PartTwoVideoTitle;
            e.Description = ShowBoilerplate;
            e.Length = PartTwoLength + TimeSpan.FromSeconds(1);
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(15));
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [SharedSubject];
        });
        var partTwoAudio = CreatePartTwoAudio();
        var partOneAudio = _fixture.CreateEpisode(e =>
        {
            e.Title = PartOneTitle;
            e.Description = ShowBoilerplate;
            e.Length = PartOneLength;
            e.ReleaseUtc = DomainTestFixture.UtcDateDaysAgo(2);
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [SharedSubject];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, partTwoAudio);
        var matched = _matcher.FindCatalogueMatchByLength(
            probe,
            [partOneAudio, partTwoAudio],
            _fixture.CreatePodcast(),
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        score.Should().BeGreaterThanOrEqualTo(CatalogueMatchScorer.MatchThreshold);
        matched.Should().BeSameAs(partTwoAudio);
    }

    [Fact(DisplayName =
        "When a candidate's only description and proper-noun overlap is the show's boilerplate repeated across the " +
        "catalogue page, the matcher rejects it, because the boilerplate is stripped before scoring and in-band " +
        "duration plus same-day release alone stay below the threshold.")]
    public void shared_show_boilerplate_is_not_description_or_proper_noun_evidence()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var probeLength = TimeSpan.FromMinutes(47);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = "Quarterly harvest ledger and the missing grain";
            e.Description = ShowBoilerplate + "\nthis week we follow the ledger trail through three villages.";
            e.Length = probeLength;
            e.ReleaseUtc = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var sameDayAudio = _fixture.CreateEpisode(e =>
        {
            e.Title = "Volcanic origami workshop goes wrong";
            e.Description = ShowBoilerplate + "\nan art class takes a turn when the kiln overheats.";
            e.Length = probeLength + TimeSpan.FromMinutes(2);
            e.ReleaseUtc = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });
        var olderAudio = Enumerable.Range(10, 2).Select(days => _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Description = ShowBoilerplate + "\nan unrelated story from an earlier week number " + days + ".";
            e.Length = probeLength + TimeSpan.FromMinutes(days);
            e.ReleaseUtc = DomainTestFixture.UtcDateDaysAgo(days);
            e.SpotifyId = _fixture.CreateSpotifyId();
        })).ToList();
        var boilerplate = CatalogueDescriptionBoilerplate.From(
            olderAudio.Append(sameDayAudio).Select(x => x.Description));

        // Act
        var rawScore = CatalogueMatchScorer.Score(probe, sameDayAudio);
        var strippedScore = CatalogueMatchScorer.Score(probe, sameDayAudio, boilerplate: boilerplate);
        var matched = _matcher.FindCatalogueMatchByLength(
            probe,
            [sameDayAudio, ..olderAudio],
            _fixture.CreatePodcast(),
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        rawScore.Should().BeGreaterThanOrEqualTo(CatalogueMatchScorer.MatchThreshold);
        strippedScore.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints + CatalogueMatchScorer.SameCalendarDayReleasePoints);
        matched.Should().BeNull();
    }

    [Fact(DisplayName =
        "Description boilerplate keeps only lines repeated in at least three catalogue descriptions, so a line two " +
        "episodes share is still evidence.")]
    public void boilerplate_needs_three_descriptions_sharing_a_line()
    {
        // Arrange
        const string everywhere = "Consider supporting the channel through the membership page.";
        const string twice = "Part two of the story continues the interview with the former member.";
        var boilerplate = CatalogueDescriptionBoilerplate.From(
        [
            everywhere + "\n" + twice,
            everywhere + "\n" + twice,
            everywhere + "\nsomething else entirely happens this week."
        ]);

        // Act
        var stripped = boilerplate.Strip(everywhere + "\n" + twice);

        // Assert
        stripped.Should().Be(twice);
    }

    [Fact(DisplayName =
        "When audio is shorter than the YouTube video and out of band, title and description evidence can still " +
        "match but the description bridge is not added, because the bridge is only for a YouTube clip of longer audio.")]
    public void description_bridge_is_not_awarded_when_audio_is_shorter_than_video()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        const string description =
            "A long conversation about a remote community, its founder, and the families who left in the nineties.";
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = "Remote community founder interview";
            e.Description = description;
            e.Length = TimeSpan.FromMinutes(90);
            e.ReleaseUtc = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var shorterAudio = _fixture.CreateEpisode(e =>
        {
            e.Title = "Remote community founder interview, edited";
            e.Description = description;
            e.Length = TimeSpan.FromMinutes(40);
            e.ReleaseUtc = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, shorterAudio);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.SameCalendarDayReleasePoints +
            CatalogueMatchScorer.SubstringTitlePoints +
            CatalogueMatchScorer.FuzzyDescriptionPoints);
    }

    [Fact(DisplayName =
        "When a YouTube clip on a 14-day publishing delay has a different title from the longer same-upload-day " +
        "Apple episode it was cut from, and only their episode-specific description matches (show boilerplate on the " +
        "page is stripped), the matcher still selects the Apple row, because the title-evidence gate (#1445) only " +
        "blocks out-of-band candidates that are not a shorter clip with a matching description.")]
    public void fourteen_day_delay_clip_with_different_title_still_matches_longer_audio()
    {
        // Arrange — existing clip rule (CatalogueMatchScorerRules) on a real catalogue page with show boilerplate.
        var youTubeRelease = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(20));
        var publishingDelay = TimeSpan.FromDays(14);
        var podcast = _fixture.CreatePodcast(p => p.YouTubePublicationOffset = publishingDelay.Ticks);
        const string episodeBlurb =
            "The crime drama SeriesName follows two women: a serial killer targeting powerful men and the " +
            "investigator tasked with tracking her down. Guest critics join the host to discuss why this " +
            "show about female rage has turned into such a hit.";
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = "Female rage on television after a major scandal";
            e.Description = ShowBoilerplate + "\n" + episodeBlurb;
            e.Length = TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(10));
            e.ReleaseUtc = youTubeRelease.Subtract(publishingDelay);
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var longerAudio = _fixture.CreateEpisode(e =>
        {
            e.Title =
                "SeriesName hits hard after a major scandal, and how gallery paintings ended up at a thrift shop";
            e.Description = ShowBoilerplate + "\n" + episodeBlurb +
                            " Plus, when a trove of art popped up at a thrift shop, people discovered it had " +
                            "been donated by a local gallery.";
            e.Length = TimeSpan.FromMinutes(24);
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(4));
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });
        var olderAudio = Enumerable.Range(20, 2).Select(days => _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Description = ShowBoilerplate + "\nan unrelated story from an earlier week number " + days + ".";
            e.Length = TimeSpan.FromMinutes(30 + days);
            e.ReleaseUtc = DomainTestFixture.UtcDateDaysAgo(days);
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        })).ToList();

        // Act
        var score = CatalogueMatchScorer.Score(probe, longerAudio);
        var matched = _matcher.FindCatalogueMatchByLength(
            probe,
            [longerAudio, ..olderAudio],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        (longerAudio.Length - probe.Length).Should()
            .BeGreaterThan(CatalogueMatchScorer.GetDurationBand(probe.Length, longerAudio.Length));
        score.Should().BeGreaterThanOrEqualTo(CatalogueMatchScorer.MatchThreshold);
        matched.Should().BeSameAs(longerAudio);
    }

    [Fact(DisplayName =
        "When the indexer reaches a weaker claimant first, Spotify audio that another YouTube-discovered episode of " +
        "the show (still without Spotify) fits better is left for that episode, because the item goes to the " +
        "highest-scoring episode in the batch.")]
    public void weaker_claimant_yields_audio_to_better_matching_rival()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var audio = CreatePartTwoAudio(title: "Order quietly removes a preacher for a year");
        var weakClaimant = _fixture.CreateEpisode(e =>
        {
            e.Title = "Former followers speak about a preacher";
            e.Description = string.Empty;
            e.Length = audio.Length + TimeSpan.FromMinutes(2);
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(9));
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [SharedSubject];
        });
        var rival = _fixture.CreateEpisode(e =>
        {
            e.Title = "Order quietly removes a preacher for a year";
            e.Description = string.Empty;
            e.Length = audio.Length + TimeSpan.FromSeconds(1);
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(15));
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [SharedSubject];
        });

        // Act
        var weakFirst = CatalogueCandidateContention.FindBetterClaimant(
            podcast, weakClaimant, [weakClaimant, rival], audio, NeedsSpotify);
        var strongFirst = CatalogueCandidateContention.FindBetterClaimant(
            podcast, rival, [weakClaimant, rival], audio, NeedsSpotify);

        // Assert
        CatalogueMatchScorer.Score(weakClaimant, audio).Should().BeGreaterThanOrEqualTo(CatalogueMatchScorer.MatchThreshold);
        weakFirst.Should().BeSameAs(rival);
        strongFirst.Should().BeNull();
    }

    [Fact(DisplayName =
        "A rival that already holds a Spotify id does not contend for the audio, because it no longer needs that platform.")]
    public void rival_with_spotify_id_does_not_contend()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var audio = CreatePartTwoAudio(title: "Order quietly removes a preacher for a year");
        var claimant = _fixture.CreateEpisode(e =>
        {
            e.Title = "Former followers speak about a preacher";
            e.Length = audio.Length + TimeSpan.FromMinutes(2);
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(9));
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [SharedSubject];
        });
        var enrichedRival = _fixture.CreateEpisode(e =>
        {
            e.Title = "Order quietly removes a preacher for a year";
            e.Length = audio.Length;
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(15));
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [SharedSubject];
        });

        // Act
        var result = CatalogueCandidateContention.FindBetterClaimant(
            podcast, claimant, [claimant, enrichedRival], audio, NeedsSpotify);

        // Assert
        result.Should().BeNull();
    }

    private static bool NeedsSpotify(Episode episode) =>
        string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode));

    private Episode CreatePartOneProbe(string title = PartOneTitle) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = title;
            e.Description = ShowBoilerplate + "\nPart one of the interview with a former follower.";
            e.Length = PartOneLength;
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-2, new TimeSpan(4, 39, 0));
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [SharedSubject];
        });

    private Episode CreatePartTwoAudio(string title = PartTwoAudioTitle) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = title;
            e.Description = ShowBoilerplate + "\nPart two of the interview with a former follower.";
            e.Length = PartTwoLength;
            e.ReleaseUtc = DomainTestFixture.UtcDateDaysAgo(1);
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [SharedSubject];
        });
}
