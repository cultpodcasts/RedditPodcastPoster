using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Merging;

/// <summary>
/// When more than one stored episode passes the platform matcher for one incoming episode, the merge
/// tie-break scores every candidate and merges into a clear winner. Ambiguity and duplicate platform ids
/// still fail loudly. A single match is never scored.
/// </summary>
public class MergeTieBreakRules
{
    private const string TikTokIntro =
        "TikTok Live chat about Sister Wives. Join me live as we chat about the new season, the family and the latest news.";

    private const string HoldingOutHelpIntro =
        "Holding Out Help supports people leaving polygamy. This week we recap Sister Wives Season 20 Episode 4.";

    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    private Podcast CreateNotesToSelf444() =>
        _fixture.CreatePodcast(p =>
        {
            p.Id = Guid.Parse("2cd74280-cc2a-4f84-9ad8-de87aa86c8ce");
            p.Name = "Notes to Self 444";
            p.EpisodeMatchRegex = string.Empty;
            p.YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks;
        });

    private Episode CreateYouTubeOnly(Podcast podcast, string youTubeId, string title, TimeSpan length,
        DateTime release, string description) =>
        _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Description = description;
            e.Length = length;
            e.ReleaseUtc = release;
            e.Subjects = [];
            EpisodeServicePresence.SetYouTubeIdentity(e, youTubeId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(youTubeId), null);
        });

    private Episode CreateIncomingSpotify(string title, TimeSpan length, DateTime release, string description)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var episode = _fixture.CreateSpotifyCatalogueEpisode(spotifyId, title, _fixture.DefaultSpotifyUrl(spotifyId),
            release, length, description);
        episode.ReleaseUtc = release;
        episode.Length = length;
        episode.Subjects = [];
        return episode;
    }

    [Fact(DisplayName =
        "Notes to Self 444 (real case 2026-10-14): a Spotify episode that fuzzy-matches two YouTube-only episodes " +
        "merges into the one with identical duration, close release and the same description intro, with no failures.")]
    public void Notes_to_Self_444_tie_breaks_to_identical_duration_close_release_candidate()
    {
        // Arrange
        var podcast = CreateNotesToSelf444();
        var tikTok = CreateYouTubeOnly(podcast, "RY7nt6KFpdA", "TikTok Live chat about Sister Wives",
            new TimeSpan(1, 35, 34), new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc), TikTokIntro);
        var recap = CreateYouTubeOnly(podcast, "BndpMN1m7oQ", "Sister Wives Season 20 E 4 Recap",
            new TimeSpan(1, 38, 12), new DateTime(2025, 10, 27, 12, 0, 0, DateTimeKind.Utc), HoldingOutHelpIntro);
        var incoming = CreateIncomingSpotify("Sister Wives New Season TikTok Live chat about",
            new TimeSpan(1, 35, 34), new DateTime(2026, 10, 14, 2, 12, 0, DateTimeKind.Utc), TikTokIntro);

        // Act
        var result = _merger.MergeEpisodes(podcast, [recap, tikTok], [incoming]);

        // Assert
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(tikTok.Id);
        EpisodeServicePresence.SpotifyEpisodeId(tikTok).Should().Be(EpisodeServicePresence.SpotifyEpisodeId(incoming));
        EpisodeServicePresence.SpotifyEpisodeId(recap).Should().BeNullOrEmpty();
    }

    [Fact(DisplayName =
        "When two candidates score near-equally the tie-break does not pick one: the merge fails and the report " +
        "names the incoming episode and each candidate's score.")]
    public void Near_equal_candidates_still_fail_and_report_names_incoming()
    {
        // Arrange
        var podcast = CreateNotesToSelf444();
        var release = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        var length = new TimeSpan(1, 0, 0);
        var first = CreateYouTubeOnly(podcast, _fixture.CreateYouTubeId(), "Weekly Live Chat", length, release, TikTokIntro);
        var second = CreateYouTubeOnly(podcast, _fixture.CreateYouTubeId(), "Weekly Live Chat", length, release, TikTokIntro);
        var incoming = CreateIncomingSpotify("Weekly Live Chat", length, release.AddHours(-1), TikTokIntro);

        // Act
        var result = _merger.MergeEpisodes(podcast, [first, second], [incoming]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().ContainSingle();
        result.FailedEpisodes.Single().Select(x => x.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        var report = result.FailedEpisodesReport();
        report.Should().Contain("Incoming: 'Weekly Live Chat'");
        report.Should().Contain($"Reason: '{EpisodeMergeCandidateScorer.BelowMinMarginReason}'");
        report.Should().Contain($"Episode-Id: '{first.Id}'");
        report.Should().Contain($"Episode-Id: '{second.Id}'");
        report.Split('\n').Where(l => l.Contains("Candidate:")).Should().HaveCount(2)
            .And.OnlyContain(l => l.Contains("score: ") && !l.Contains("score: n/a"));
    }

    [Fact(DisplayName =
        "Surviving the Black Church (real case): two stored episodes sharing YouTube id Wm-fDWIIZr0 are a data " +
        "problem, so an incoming episode with that id still fails and is never tie-broken.")]
    public void Duplicate_youtube_id_stored_episodes_still_fail()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Name = "Surviving the Black Church Podcast");
        var release = new DateTime(2026, 10, 1, 17, 0, 0, DateTimeKind.Utc);
        var length = new TimeSpan(0, 52, 10);
        var original = CreateYouTubeOnly(podcast, "Wm-fDWIIZr0", "Leaving the church: a survivor story", length, release, "Story");
        var duplicate = CreateYouTubeOnly(podcast, "Wm-fDWIIZr0", "Leaving the church", length.Add(TimeSpan.FromMinutes(3)),
            release.AddDays(-200), "Other");
        var incoming = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = "Leaving the church: a survivor story";
            e.Length = length;
            e.ReleaseUtc = release;
            e.Subjects = [];
            EpisodeServicePresence.SetYouTubeIdentity(e, "Wm-fDWIIZr0");
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl("Wm-fDWIIZr0"), null);
        });

        // Act
        var result = _merger.MergeEpisodes(podcast, [original, duplicate], [incoming]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().ContainSingle();
        result.FailedEpisodes.Single().Should().HaveCount(2);
        result.FailedMerges.Should().ContainSingle()
            .Which.Reason.Should().Be(EpisodeMergeCandidateScorer.DuplicatePlatformIdReason);
    }

    [Fact(DisplayName =
        "A single matching stored episode merges exactly as before, even when its tie-break score would be low.")]
    public void Single_match_is_unchanged_and_never_scored()
    {
        // Arrange
        var podcast = CreateNotesToSelf444();
        var recap = CreateYouTubeOnly(podcast, "BndpMN1m7oQ", "Sister Wives Season 20 E 4 Recap",
            new TimeSpan(1, 38, 12), new DateTime(2025, 10, 27, 12, 0, 0, DateTimeKind.Utc), HoldingOutHelpIntro);
        var incoming = CreateIncomingSpotify("Sister Wives Season 20 E 4 Recap",
            new TimeSpan(1, 38, 12), new DateTime(2026, 10, 14, 2, 12, 0, DateTimeKind.Utc), TikTokIntro);

        // Act
        var result = _merger.MergeEpisodes(podcast, [recap], [incoming]);

        // Assert
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(recap.Id);
    }
}
