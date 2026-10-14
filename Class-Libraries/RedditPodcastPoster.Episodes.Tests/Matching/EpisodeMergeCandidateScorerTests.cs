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

    [Fact(DisplayName = "Default options are valid and weights total 100.")]
    public void Defaults_are_valid()
    {
        var options = new EpisodeMergeTieBreakOptions();
        EpisodeMergeTieBreakOptions.IsValid(options).Should().BeTrue();
        options.MaxScore.Should().Be(100);
    }
}
