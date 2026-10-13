using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching.Candidates;

/// <summary>
/// One candidate-sourcing policy for every audio platform. Each case runs identically for Spotify and Apple
/// identities through the shared <see cref="IEpisodePlatformMatcher"/>.
/// </summary>
public class CatalogueCandidatePolicyRules
{
    private static readonly DateTime YouTubeRelease = new(2026, 10, 12, 8, 0, 39, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly DomainTestFixture _fixture = new();
    private readonly IEpisodePlatformMatcher _matcher = EpisodeDomainTestServices.CreatePlatformMatcher();
    private readonly CatalogueCandidatePolicy _sut = new(new FixedTimeProvider(Now));

    public static TheoryData<string> Platforms => new() { "spotify", "apple" };

    [Theory(DisplayName =
        "When a recent YouTube episode's title differs from the audio-feed title (Steve Hassan / Ben McKenzie, 12 Oct 2026), " +
        "FindMatch finds the audio episode in the show's newest page without searching, on every platform, " +
        "because the show feed does not depend on the incoming title.")]
    [MemberData(nameof(Platforms))]
    public async Task Real_divergent_title_pair_matches_from_recent_show_feed(string platform)
    {
        // Arrange
        var probe = BenMcKenzieYouTubeProbe();
        var audio = Identify(platform, BenMcKenzieAudio());
        var previousWeek = Identify(platform, Audio(_fixture.CreateShortTitle(), new TimeSpan(0, 58, 10), YouTubeRelease.AddDays(-7)));
        var source = new FakeSource(platform, hasShow: true, recent: [audio, previousWeek], search: []);

        // Act
        var result = await _sut.FindMatch(probe.ReleaseUtc, source, c => Match(probe, c));

        // Assert
        result.Should().BeSameAs(audio);
        source.RecentCalls.Should().Be(1);
        source.SearchCalls.Should().Be(0);
        source.RequestedLimit.Should().Be(CatalogueCandidatePolicy.RecentShowEpisodesLimit);
    }

    [Theory(DisplayName =
        "When the episode was released outside the recent window, FindMatch skips the show's newest page and uses the " +
        "platform search fallback, on every platform, because one recent page cannot contain an older episode.")]
    [MemberData(nameof(Platforms))]
    public async Task Older_episode_uses_search_fallback(string platform)
    {
        // Arrange
        var title = _fixture.CreateShortTitle();
        var release = Now.AddDays(-120);
        var probe = Audio(title, TimeSpan.FromMinutes(50), release);
        var older = Identify(platform, Audio(title, TimeSpan.FromMinutes(50), release));
        var source = new FakeSource(platform, hasShow: true, recent: [], search: [older]);

        // Act
        var result = await _sut.FindMatch(probe.ReleaseUtc, source, c => Match(probe, c));

        // Assert
        result.Should().BeSameAs(older);
        source.RecentCalls.Should().Be(0);
        source.SearchCalls.Should().Be(1);
    }

    [Theory(DisplayName =
        "When the platform show id is unknown, FindMatch does not read a show feed and goes straight to search, " +
        "on every platform, because there is no feed to read.")]
    [MemberData(nameof(Platforms))]
    public async Task No_show_id_goes_straight_to_search(string platform)
    {
        // Arrange
        var probe = BenMcKenzieYouTubeProbe();
        var source = new FakeSource(platform, hasShow: false, recent: [Identify(platform, BenMcKenzieAudio())], search: []);

        // Act
        var result = await _sut.FindMatch(probe.ReleaseUtc, source, c => Match(probe, c));

        // Assert
        result.Should().BeNull();
        source.RecentCalls.Should().Be(0);
        source.SearchCalls.Should().Be(1);
    }

    [Theory(DisplayName =
        "When the recent page has no match, FindMatch falls back to search and does not re-match candidates already tried, " +
        "on every platform, so a recent episode outside the newest page is still found.")]
    [MemberData(nameof(Platforms))]
    public async Task Recent_page_miss_falls_back_to_search_without_retrying(string platform)
    {
        // Arrange
        var title = _fixture.CreateShortTitle();
        var probe = Audio(title, TimeSpan.FromMinutes(45), Now.AddDays(-3));
        var unrelated = Identify(platform, Audio(_fixture.CreateShortTitle(), TimeSpan.FromMinutes(20), Now.AddDays(-1)));
        var target = Identify(platform, Audio(title, TimeSpan.FromMinutes(45), Now.AddDays(-3)));
        var source = new FakeSource(platform, hasShow: true, recent: [unrelated], search: [unrelated, target]);
        var batches = new List<IReadOnlyList<Episode>>();

        // Act
        var result = await _sut.FindMatch(probe.ReleaseUtc, source, c =>
        {
            batches.Add(c);
            return Match(probe, c);
        });

        // Assert
        result.Should().BeSameAs(target);
        batches.Should().HaveCount(2);
        batches[1].Should().Equal(target);
    }

    private Task<Episode?> Match(Episode probe, IReadOnlyList<Episode> candidates) =>
        Task.FromResult(_matcher.FindCatalogueMatchByLength(
            probe,
            candidates,
            new Podcast { ReleaseAuthority = Service.YouTube },
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(ReleaseAuthority: Service.YouTube, EnrichingYouTubeDiscoveredEpisode: true)));

    private static Episode BenMcKenzieYouTubeProbe() => new()
    {
        Title = "Why Is Everyone Lying to You for Money? The Great Crypto Scam EXPOSED (w/ Ben McKenzie)",
        Length = new TimeSpan(0, 56, 49),
        ReleaseUtc = YouTubeRelease,
        Subjects = ["Cult Psychology", "Multi-Level Marketing"]
    };

    private static Episode BenMcKenzieAudio()
    {
        var audio = Audio(
            "Everyone Is Lying to You for Money, Crypto and Cult Psychology with Ben McKenzie: My conversation with the author of Easy Money about financial manipulation, the need to belong, and protecting our freedom of mind.",
            new TimeSpan(0, 59, 29),
            new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc));
        audio.Subjects = ["Cult Psychology", "Multi-Level Marketing"];
        return audio;
    }

    private static Episode Audio(string title, TimeSpan length, DateTime release) =>
        new() { Title = title, Length = length, ReleaseUtc = release, Description = string.Empty };

    private Episode Identify(string platform, Episode episode)
    {
        if (platform == "spotify")
        {
            EpisodeServicePresence.SetSpotifyIdentity(episode, _fixture.CreateSpotifyId());
        }
        else
        {
            episode.AppleId = _fixture.CreateAppleId();
        }

        return episode;
    }

    private sealed class FakeSource(string platform, bool hasShow, IReadOnlyList<Episode> recent, IReadOnlyList<Episode> search)
        : ICatalogueCandidateSource<Episode>
    {
        public int RecentCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public int? RequestedLimit { get; private set; }
        public bool HasShow => hasShow;

        public Task<IReadOnlyList<Episode>> GetRecentShowEpisodes(int limit)
        {
            RecentCalls++;
            RequestedLimit = limit;
            return Task.FromResult<IReadOnlyList<Episode>>(recent.Take(limit).ToList());
        }

        public Task<IReadOnlyList<Episode>> SearchEpisodes()
        {
            SearchCalls++;
            return Task.FromResult(search);
        }

        public string GetId(Episode item) => platform == "spotify"
            ? EpisodeServicePresence.SpotifyEpisodeId(item)!
            : item.AppleId!.Value.ToString();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
