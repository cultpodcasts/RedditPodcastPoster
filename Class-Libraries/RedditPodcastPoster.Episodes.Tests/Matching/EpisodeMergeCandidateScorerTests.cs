using FluentAssertions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.Matching;

/// <summary>
/// Edge cases of the merge tie-break scorer: disabled, duplicate platform ids, an incoming id already
/// carried by a candidate, below MinScore, below MinMargin, graded signals and options validation.
/// </summary>
public class EpisodeMergeCandidateScorerTests
{
    private static readonly DateTime Release = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
    private readonly DomainTestFixture _fixture = new();
    private readonly Podcast _podcast;

    public EpisodeMergeCandidateScorerTests()
    {
        _podcast = _fixture.CreatePodcast(p => p.YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks);
    }

    private static EpisodeMergeCandidateScorer CreateScorer(Action<EpisodeMergeTieBreakOptions>? configure = null)
    {
        var options = new EpisodeMergeTieBreakOptions();
        configure?.Invoke(options);
        return new EpisodeMergeCandidateScorer(Options.Create(options));
    }

    private Episode YouTube(string title, TimeSpan length, DateTime release, string description = "", string? id = null) =>
        _fixture.CreateStoredEpisode(_podcast, e =>
        {
            var youTubeId = id ?? _fixture.CreateYouTubeId();
            e.Title = title;
            e.Length = length;
            e.ReleaseUtc = release;
            e.Description = description;
            e.Subjects = [];
            EpisodeServicePresence.SetYouTubeIdentity(e, youTubeId);
        });

    private Episode Spotify(string title, TimeSpan length, DateTime release, string description = "") =>
        _fixture.CreateStoredEpisode(_podcast, e =>
        {
            e.Title = title;
            e.Length = length;
            e.ReleaseUtc = release;
            e.Description = description;
            e.Subjects = [];
            EpisodeServicePresence.SetSpotifyIdentity(e, _fixture.CreateSpotifyId());
        });

    [Fact(DisplayName = "Notes to Self 444 real case pinned at scorer level: RY7nt6KFpdA reaches MinScore and leads BndpMN1m7oQ by at least MinMargin.")]
    public void Notes_to_Self_444_scores_clear_winner()
    {
        var options = new EpisodeMergeTieBreakOptions();
        const string tikTok = "TikTok Live chat about Sister Wives. Join me live as we chat about the new season, the family and the latest news.";
        var ry7 = YouTube("TikTok Live chat about Sister Wives", new TimeSpan(1, 35, 34), Release, tikTok, "RY7nt6KFpdA");
        var recap = YouTube("Sister Wives Season 20 E 4 Recap", new TimeSpan(1, 38, 12),
            new DateTime(2025, 10, 27, 12, 0, 0, DateTimeKind.Utc),
            "Holding Out Help supports people leaving polygamy. This week we recap Sister Wives Season 20 Episode 4.",
            "BndpMN1m7oQ");
        var incoming = Spotify("Sister Wives New Season TikTok Live chat about", new TimeSpan(1, 35, 34),
            new DateTime(2026, 10, 14, 2, 12, 0, DateTimeKind.Utc), tikTok);

        var decision = CreateScorer().Decide(_podcast, incoming, [recap, ry7]);

        decision.Winner.Should().BeSameAs(ry7);
        decision.Reason.Should().Be(EpisodeMergeCandidateScorer.TieBrokenReason);
        decision.Scores[0].Score.Should().BeGreaterThanOrEqualTo(options.MinScore);
        (decision.Scores[0].Score - decision.Scores[1].Score).Should().BeGreaterThanOrEqualTo(options.MinMargin);
    }

    [Fact(DisplayName = "Fewer than two candidates is a caller error.")]
    public void Fewer_than_two_candidates_throws()
    {
        var a = YouTube("Live chat", TimeSpan.FromHours(1), Release);
        var incoming = Spotify("Live chat", TimeSpan.FromHours(1), Release);

        var act = () => CreateScorer().Decide(_podcast, incoming, [a]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Disabled: never picks a winner and does not score.")]
    public void Disabled_returns_no_winner()
    {
        var a = YouTube("Live chat", TimeSpan.FromHours(1), Release);
        var b = YouTube("Other", TimeSpan.FromHours(2), Release.AddYears(-1));
        var incoming = Spotify("Live chat", TimeSpan.FromHours(1), Release.AddHours(-1));

        var decision = CreateScorer(o => o.Enabled = false).Decide(_podcast, incoming, [a, b]);

        decision.Winner.Should().BeNull();
        decision.Scores.Should().BeEmpty();
        decision.Reason.Should().Be(EpisodeMergeCandidateScorer.DisabledReason);
    }

    [Fact(DisplayName = "Candidates sharing a YouTube id are a data problem: no winner, not scored.")]
    public void Duplicate_platform_id_candidates_fail()
    {
        var a = YouTube("Live chat", TimeSpan.FromHours(1), Release, id: "Wm-fDWIIZr0");
        var b = YouTube("Other", TimeSpan.FromHours(2), Release.AddYears(-1), id: "Wm-fDWIIZr0");
        var incoming = Spotify("Live chat", TimeSpan.FromHours(1), Release.AddHours(-1));

        var decision = CreateScorer().Decide(_podcast, incoming, [a, b]);

        decision.Winner.Should().BeNull();
        decision.Reason.Should().Be(EpisodeMergeCandidateScorer.DuplicatePlatformIdReason);
    }

    [Fact(DisplayName = "A candidate already carrying the incoming platform id is never out-scored by fuzzy evidence.")]
    public void Candidate_with_incoming_platform_id_fails()
    {
        var a = YouTube("Live chat", TimeSpan.FromHours(1), Release, id: "abcDEF12345");
        var b = YouTube("Other", TimeSpan.FromHours(2), Release.AddYears(-1));
        var incoming = YouTube("Live chat", TimeSpan.FromHours(1), Release, id: "abcDEF12345");

        var decision = CreateScorer().Decide(_podcast, incoming, [a, b]);

        decision.Winner.Should().BeNull();
        decision.Reason.Should().Be(EpisodeMergeCandidateScorer.PlatformIdMatchReason);
    }

    [Fact(DisplayName = "A clear leader below MinScore is not chosen.")]
    public void Leader_below_min_score_fails()
    {
        var a = YouTube("Alpha", TimeSpan.FromHours(1), Release.AddYears(-1));
        var b = YouTube("Zulu zulu", TimeSpan.FromHours(3), Release.AddYears(-2));
        var incoming = Spotify("Alpha", TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(4)), Release);

        var decision = CreateScorer().Decide(_podcast, incoming, [a, b]);

        decision.Winner.Should().BeNull();
        decision.Reason.Should().Be(EpisodeMergeCandidateScorer.BelowMinScoreReason);
        decision.Scores.Should().HaveCount(2);
        decision.Scores[0].Score.Should().BeLessThan(new EpisodeMergeTieBreakOptions().MinScore);
    }

    [Fact(DisplayName = "Two strong candidates within MinMargin of each other are not chosen.")]
    public void Close_candidates_fail_on_margin()
    {
        var a = YouTube("Weekly Live Chat", TimeSpan.FromHours(1), Release, "Intro");
        var b = YouTube("Weekly Live Chat", TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(30)), Release.AddDays(1), "Intro");
        var incoming = Spotify("Weekly Live Chat", TimeSpan.FromHours(1), Release.AddHours(-1), "Intro");

        var decision = CreateScorer().Decide(_podcast, incoming, [a, b]);

        decision.Winner.Should().BeNull();
        decision.Reason.Should().Be(EpisodeMergeCandidateScorer.BelowMinMarginReason);
    }

    [Fact(DisplayName = "Release proximity is measured after the YouTube publication offset: exact alignment earns full release points.")]
    public void Release_proximity_uses_publication_offset()
    {
        var scorer = CreateScorer(o =>
        {
            o.DurationWeight = 0;
            o.TitleWeight = 0;
            o.DescriptionWeight = 0;
            o.SubjectWeight = 0;
            o.MinScore = 0;
        });
        var youTube = YouTube("x", TimeSpan.FromHours(1), Release);

        scorer.Score(_podcast, youTube, Spotify("x", TimeSpan.FromHours(1), Release.AddHours(-1))).Should().Be(30);
        scorer.Score(_podcast, youTube, Spotify("x", TimeSpan.FromHours(1), Release.AddDays(-15).AddHours(-1))).Should().Be(15);
        scorer.Score(_podcast, youTube, Spotify("x", TimeSpan.FromHours(1), Release.AddDays(-40))).Should().Be(0);
    }

    [Fact(DisplayName = "Duration closeness is graded linearly to zero at DurationWindow.")]
    public void Duration_is_graded()
    {
        var scorer = CreateScorer(o =>
        {
            o.ReleaseWeight = 0;
            o.TitleWeight = 0;
            o.DescriptionWeight = 0;
            o.SubjectWeight = 0;
            o.MinScore = 0;
        });
        var youTube = YouTube("x", TimeSpan.FromHours(1), Release);

        scorer.Score(_podcast, youTube, Spotify("x", TimeSpan.FromHours(1), Release)).Should().Be(30);
        scorer.Score(_podcast, youTube, Spotify("x", TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(2.5)), Release)).Should().Be(15);
        scorer.Score(_podcast, youTube, Spotify("x", TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(6)), Release)).Should().Be(0);
    }

    [Theory(DisplayName = "Options validation rejects negative weights, non-positive windows and MinScore above the maximum.")]
    [InlineData("negativeWeight")]
    [InlineData("zeroWindow")]
    [InlineData("minScoreAboveMax")]
    [InlineData("negativeMargin")]
    public void Invalid_options_are_rejected(string scenario)
    {
        var options = new EpisodeMergeTieBreakOptions();
        switch (scenario)
        {
            case "negativeWeight": options.TitleWeight = -1; break;
            case "zeroWindow": options.ReleaseWindow = TimeSpan.Zero; break;
            case "minScoreAboveMax": options.MinScore = options.MaxScore + 1; break;
            case "negativeMargin": options.MinMargin = -1; break;
        }

        EpisodeMergeTieBreakOptions.IsValid(options).Should().BeFalse();
    }

    [Fact(DisplayName = "Single heuristic match: below SingleHeuristicMinScore is rejected with its score; at or above is accepted.")]
    public void Single_heuristic_threshold()
    {
        var youTube = YouTube("Live chat", TimeSpan.FromHours(1), Release);
        var strong = Spotify("Live chat", TimeSpan.FromHours(1), Release.AddHours(-1));
        var weak = Spotify("Zulu", TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(4)), Release.AddYears(-1));
        var scorer = CreateScorer();

        var accepted = scorer.DecideSingleHeuristic(_podcast, strong, youTube);
        var rejected = scorer.DecideSingleHeuristic(_podcast, weak, youTube);

        accepted.Accepted.Should().BeTrue();
        accepted.Reason.Should().Be(EpisodeMergeCandidateScorer.SingleAcceptedReason);
        rejected.Accepted.Should().BeFalse();
        rejected.Score.Should().BeLessThan(new EpisodeMergeTieBreakOptions().SingleHeuristicMinScore);
        rejected.Reason.Should().Be(EpisodeMergeCandidateScorer.SingleRejectedReason);
    }

    [Theory(DisplayName = "Single heuristic scoring is skipped (accepted, unscored) when the tie-break or the toggle is off.")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Single_heuristic_disabled_accepts_unscored(bool enabled, bool scoreSingles)
    {
        var youTube = YouTube("Live chat", TimeSpan.FromHours(1), Release);
        var weak = Spotify("Zulu", TimeSpan.FromHours(2), Release.AddYears(-1));

        var decision = CreateScorer(o =>
        {
            o.Enabled = enabled;
            o.ScoreSingleHeuristicMatches = scoreSingles;
        }).DecideSingleHeuristic(_podcast, weak, youTube);

        decision.Accepted.Should().BeTrue();
        decision.Score.Should().BeNull();
        decision.Reason.Should().Be(EpisodeMergeCandidateScorer.SingleScoringDisabledReason);
    }

    [Fact(DisplayName = "SingleHeuristicMinScore outside 0..MaxScore fails validation.")]
    public void Single_heuristic_min_score_validated()
    {
        var options = new EpisodeMergeTieBreakOptions();
        options.SingleHeuristicMinScore = options.MaxScore + 1;
        EpisodeMergeTieBreakOptions.IsValid(options).Should().BeFalse();
        options.SingleHeuristicMinScore = -1;
        EpisodeMergeTieBreakOptions.IsValid(options).Should().BeFalse();
    }

    [Fact(DisplayName = "Default options are valid and weights total 100.")]
    public void Defaults_are_valid()
    {
        var options = new EpisodeMergeTieBreakOptions();
        EpisodeMergeTieBreakOptions.IsValid(options).Should().BeTrue();
        options.MaxScore.Should().Be(100);
    }
}
