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
        "EpisodeMostRecent.OfAsync returns null for an empty stream, because a podcast with no episodes has no latest release.")]
    public async Task Empty_stream_has_no_most_recent()
    {
        // Arrange
        var none = EmptyEpisodeStream();

        // Act
        var mostRecent = await EpisodeMostRecent.OfAsync(none);

        // Assert
        mostRecent.Should().BeNull();
    }

    [Fact(DisplayName =
        "EpisodeMostRecent.OfAsync returns the episode with the latest ReleaseUtc from a stream, because Cosmos scans the partition and the winner is folded in process (2206).")]
    public async Task Stream_picks_latest_ReleaseUtc_without_buffering_requirement()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var older = _fixture.CreateStoredEpisode(podcast, episode =>
            episode.ReleaseUtc = DomainTestFixture.UtcDaysAgo(10));
        var newer = _fixture.CreateStoredEpisode(podcast, episode =>
            episode.ReleaseUtc = DomainTestFixture.UtcDaysAgo(1));

        // Act
        var mostRecent = await EpisodeMostRecent.OfAsync(EpisodeStream(older, newer));

        // Assert
        mostRecent.Should().NotBeNull();
        mostRecent!.Id.Should().Be(newer.Id);
    }

    private static async IAsyncEnumerable<Episode> EmptyEpisodeStream()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<Episode> EpisodeStream(params Episode[] episodes)
    {
        foreach (var episode in episodes)
        {
            yield return episode;
            await Task.Yield();
        }
    }
}
