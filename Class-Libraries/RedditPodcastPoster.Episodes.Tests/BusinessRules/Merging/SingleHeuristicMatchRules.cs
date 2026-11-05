using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.EpisodeLookup.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Merging;

/// <summary>
/// A single match reached by a heuristic path (fuzzy title + duration, or release + duration) is scored and
/// rejected below the minimum, so a lookalike never absorbs a new episode. Certain matches (platform id,
/// exact title with day+duration, EpisodeMatchRegex) merge unscored as before.
/// </summary>
public class SingleHeuristicMatchRules
{
    private const string TikTokIntro =
        "TikTok Live chat about Sister Wives. Join me live as we chat about the new season, the family and the latest news.";

    private const string HoldingOutHelpIntro =
        "Holding Out Help supports people leaving polygamy. This week we recap Sister Wives Season 20 Episode 4.";

    private readonly DomainTestFixture _fixture = new();
    private readonly DateTime _incomingRelease = DomainTestFixture.UtcAtTime(-2, new TimeSpan(2, 12, 0));
    private readonly DateTime _yearOldRelease = DomainTestFixture.UtcAtTime(-365, TimeSpan.FromHours(12));

    private Podcast NotesToSelf444(string? episodeMatchRegex = null) =>
        _fixture.CreatePodcast(p =>
        {
            p.Id = _fixture.CreateGuid();
            p.Name = "Notes to Self 444";
            p.EpisodeMatchRegex = episodeMatchRegex ?? string.Empty;
            p.YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks;
        });

    private Episode YouTubeOnly(Podcast podcast, string youTubeId, string title, TimeSpan length, DateTime release,
        string description) =>
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

    private Episode IncomingSpotify(string title, TimeSpan length, DateTime release, string description)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var episode = _fixture.CreateSpotifyCatalogueEpisode(spotifyId, title, _fixture.DefaultSpotifyUrl(spotifyId),
            release, length, description);
        episode.ReleaseUtc = release;
        episode.Length = length;
        episode.Subjects = [];
        return episode;
    }

    private static EpisodeMerger Merger(Action<EpisodeMergeTieBreakOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddEpisodesDomain().AddEpisodeLookup();
        if (configure != null)
        {
            services.Configure(configure);
        }

        var provider = services.BuildServiceProvider();
        return new EpisodeMerger(
            provider.GetRequiredService<IEpisodePlatformMatcher>(),
            provider.GetRequiredService<IEpisodePlatformMerger>(),
            provider.GetRequiredService<IEpisodeMergeCandidateScorer>(),
            NullLogger<EpisodeMerger>.Instance);
    }

    private Episode WeakLookalikeRecap(Podcast podcast) =>
        YouTubeOnly(podcast, "BndpMN1m7oQ", "Sister Wives Season 20 E 4 Recap", new TimeSpan(1, 38, 12),
            _yearOldRelease, HoldingOutHelpIntro);

    private Episode TikTokLiveIncoming() =>
        IncomingSpotify("Sister Wives New Season TikTok Live chat about", new TimeSpan(1, 35, 34),
            _incomingRelease, TikTokIntro);

    [Fact(DisplayName =
        "A year-old lookalike with a near title and similar duration does not Heuristic-match without " +
        "release alignment, so the incoming episode is added as new and the lookalike is untouched.")]
    public void Weak_lookalike_single_heuristic_match_is_rejected()
    {
        // Arrange
        var podcast = NotesToSelf444();
        var recap = WeakLookalikeRecap(podcast);
        var incoming = TikTokLiveIncoming();

        // Act
        var result = Merger().MergeEpisodes(podcast, [recap], [incoming]);

        // Assert
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle().Which.Should().BeSameAs(incoming);
        EpisodeServicePresence.SpotifyEpisodeId(recap).Should().BeNullOrEmpty();
    }

    [Fact(DisplayName =
        "A genuine cross-platform single match (YouTube vs Spotify, same duration within seconds, released within the " +
        "publication offset, reordered title) still merges.")]
    public void Genuine_cross_platform_single_heuristic_match_still_merges()
    {
        // Arrange
        var podcast = NotesToSelf444();
        var youTube = YouTubeOnly(podcast, "RY7nt6KFpdA", "TikTok Live chat about Sister Wives",
            new TimeSpan(1, 35, 34), _incomingRelease.AddHours(1), TikTokIntro);
        var incoming = IncomingSpotify("Sister Wives New Season TikTok Live chat about", new TimeSpan(1, 35, 31),
            _incomingRelease, TikTokIntro);

        // Act
        var result = Merger().MergeEpisodes(podcast, [youTube], [incoming]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle().Which.Existing.Should().BeSameAs(youTube);
    }

    [Fact(DisplayName =
        "An exact title with a far release does not Certain-merge — the incoming episode is added " +
        "and the lookalike is untouched, because title alone must not override the calendar-day gate.")]
    public void Exact_title_far_release_does_not_certain_merge()
    {
        // Arrange
        var podcast = NotesToSelf444();
        var recap = WeakLookalikeRecap(podcast);
        var incoming = IncomingSpotify(recap.Title, new TimeSpan(1, 38, 12),
            _incomingRelease, TikTokIntro);

        // Act
        var result = Merger().MergeEpisodes(podcast, [recap], [incoming]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle().Which.Should().BeSameAs(incoming);
        EpisodeServicePresence.SpotifyEpisodeId(recap).Should().BeNullOrEmpty();
    }

    [Fact(DisplayName =
        "A certain single match by the podcast's EpisodeMatchRegex merges unscored, even when every scored signal is weak.")]
    public void Episode_match_regex_single_match_is_unchanged()
    {
        // Arrange
        var podcast = NotesToSelf444(@"Episode (?'episodematch'\d+)");
        var stored = YouTubeOnly(podcast, "abcDEF12345", "Episode 12 - The first title", new TimeSpan(0, 40, 0),
            _yearOldRelease, "One");
        var incoming = IncomingSpotify("Episode 12 - Renamed later", new TimeSpan(0, 40, 30),
            _incomingRelease, "Two");

        // Act
        var result = Merger().MergeEpisodes(podcast, [stored], [incoming]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle().Which.Existing.Should().BeSameAs(stored);
    }

    [Fact(DisplayName =
        "With ScoreSingleHeuristicMatches off, a release-aligned fuzzy+duration heuristic match " +
        "merges unscored (year-old lookalikes no longer Heuristic-match without release alignment).")]
    public void Toggle_off_restores_previous_behaviour()
    {
        // Arrange — genuine cross-platform pair (aligned release); toggle only disables scoring.
        var podcast = NotesToSelf444();
        var youTube = YouTubeOnly(podcast, "RY7nt6KFpdA", "TikTok Live chat about Sister Wives",
            new TimeSpan(1, 35, 34), _incomingRelease.AddHours(1), TikTokIntro);
        var incoming = IncomingSpotify("Sister Wives New Season TikTok Live chat about", new TimeSpan(1, 35, 31),
            _incomingRelease, TikTokIntro);

        // Act
        var result = Merger(o => o.ScoreSingleHeuristicMatches = false)
            .MergeEpisodes(podcast, [youTube], [incoming]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle().Which.Existing.Should().BeSameAs(youTube);
    }
}
