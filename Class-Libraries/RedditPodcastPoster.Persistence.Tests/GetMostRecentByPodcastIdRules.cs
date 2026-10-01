using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;

namespace RedditPodcastPoster.Persistence.Tests;

public class GetMostRecentByPodcastIdRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "GetMostRecentByPodcastId returns the episode with the latest ReleaseUtc, including a document that only had legacy release JSON, because Cosmos cannot ORDER BY the dual-key ternary (2206).")]
    public async Task Most_recent_uses_ReleaseUtc_after_deserialize_including_legacy_release_only_json()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var older = _fixture.CreateStoredEpisode(podcast, episode =>
            episode.ReleaseUtc = DomainTestFixture.UtcDaysAgo(10));
        var newerUtc = DomainTestFixture.UtcAtTime(-1, new TimeSpan(12, 0, 0));
        var newerIso = newerUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var newer = JsonSerializer.Deserialize<Episode>(
            $"{{\"release\":\"{newerIso}\"}}",
            options)!;
        newer.PodcastId = podcast.Id;
        var otherShow = _fixture.CreateStoredEpisode(_fixture.CreatePodcast(), episode =>
            episode.ReleaseUtc = DomainTestFixture.UtcDaysAgo(0));
        var repository = new InMemoryEpisodeRepository();
        repository.Seed(older, newer, otherShow);

        // Act
        var mostRecent = await repository.GetMostRecentByPodcastId(podcast.Id);

        // Assert
        mostRecent.Should().NotBeNull();
        mostRecent!.Id.Should().Be(newer.Id);
        mostRecent.ReleaseUtc.Should().Be(newerUtc);
    }

    [Fact(DisplayName =
        "EpisodeMostRecent.Of returns null for an empty set, because a podcast with no episodes has no latest release.")]
    public void Empty_set_has_no_most_recent()
    {
        // Arrange
        IEnumerable<Episode> none = [];

        // Act
        var mostRecent = EpisodeMostRecent.Of(none);

        // Assert
        mostRecent.Should().BeNull();
    }

    [Fact(DisplayName =
        "Cosmos EpisodeRepository.GetMostRecentByPodcastId must not ORDER BY the dual-key ternary, because Cosmos error 2206 rejects computed ORDER BY expressions.")]
    public void Cosmos_most_recent_scans_then_picks_in_process()
    {
        // Arrange
        var source = File.ReadAllText(LocateEpisodeRepositorySource());
        var start = source.IndexOf("public async Task<Episode?> GetMostRecentByPodcastId", StringComparison.Ordinal);
        var next = source.IndexOf("public async Task Save(Episode episode)", start, StringComparison.Ordinal);
        var method = source[start..next];

        // Act
        var ordersByTernary = method.Contains("OrderByDescending", StringComparison.Ordinal);
        var usesInProcessPick = method.Contains("EpisodeMostRecent.Of", StringComparison.Ordinal);

        // Assert
        ordersByTernary.Should().BeFalse();
        usesInProcessPick.Should().BeTrue();
    }

    private static string LocateEpisodeRepositorySource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "Class-Libraries",
                "RedditPodcastPoster.Persistence",
                "Repositories",
                "EpisodeRepository.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("EpisodeRepository.cs was not found walking up from the test output directory.");
    }
}
