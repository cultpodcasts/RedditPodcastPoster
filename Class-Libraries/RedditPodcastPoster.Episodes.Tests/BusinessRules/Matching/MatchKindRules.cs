using FluentAssertions;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// <see cref="IEpisodePlatformMatcher.Classify"/>: certain paths (platform id, exact title, EpisodeMatchRegex) are
/// never scored by the merge; heuristic paths (fuzzy title + duration, release + duration) may be.
/// </summary>
public class MatchKindRules
{
    private static readonly DateTime Release = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly DomainTestFixture _fixture = new();
    private readonly IEpisodePlatformMatcher _matcher = EpisodeDomainTestServices.CreatePlatformMatcher();
    private readonly Podcast _podcast;

    public MatchKindRules() => _podcast = _fixture.CreatePodcast();

    private Episode YouTube(string id, string title, TimeSpan length) =>
        _fixture.CreateStoredEpisode(_podcast, e =>
        {
            e.Title = title;
            e.Length = length;
            e.ReleaseUtc = Release;
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
    public void Same_youtube_id_is_certain() =>
        Classify(YouTube("abcDEF12345", "A", TimeSpan.FromHours(1)), YouTube("abcDEF12345", "B", TimeSpan.FromHours(2)))
            .Should().Be(EpisodeMatchKind.Certain);

    [Fact(DisplayName = "Different YouTube ids never match.")]
    public void Different_youtube_ids_are_none() =>
        Classify(YouTube("abcDEF12345", "Same", TimeSpan.FromHours(1)), YouTube("zyxWVU98765", "Same", TimeSpan.FromHours(1)))
            .Should().Be(EpisodeMatchKind.None);

    [Fact(DisplayName = "An exact title is a certain match, whatever the release and duration.")]
    public void Exact_title_is_certain() =>
        Classify(YouTube("abcDEF12345", "Exact Title", TimeSpan.FromHours(1)),
                Spotify("Exact Title", TimeSpan.FromHours(3), Release.AddYears(-2)))
            .Should().Be(EpisodeMatchKind.Certain);

    [Fact(DisplayName = "An EpisodeMatchRegex episode number match is certain.")]
    public void Episode_match_regex_is_certain() =>
        Classify(YouTube("abcDEF12345", "Episode 7 - old", TimeSpan.FromHours(1)),
                Spotify("Episode 7 - new", TimeSpan.FromHours(3), Release.AddYears(-2)),
                new Regex(@"Episode (?'episodematch'\d+)", Podcast.EpisodeMatchFlags))
            .Should().Be(EpisodeMatchKind.Certain);

    [Fact(DisplayName = "A fuzzy title with a duration within tolerance is a heuristic match.")]
    public void Fuzzy_title_and_duration_is_heuristic() =>
        Classify(YouTube("abcDEF12345", "TikTok Live chat about Sister Wives", TimeSpan.FromMinutes(95)),
                Spotify("Sister Wives New Season TikTok Live chat about", TimeSpan.FromMinutes(95), Release.AddDays(-30)))
            .Should().Be(EpisodeMatchKind.Heuristic);
}
