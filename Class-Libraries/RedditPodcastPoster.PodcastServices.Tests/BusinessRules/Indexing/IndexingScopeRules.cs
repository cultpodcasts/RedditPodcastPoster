using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class IndexingScopeRules
{
    private static readonly DateTime ReleasedSince = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EpisodeRelease = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName =
        "Episodes below minimum duration are marked ignored during indexing.")]
    public async Task short_discovered_episodes_are_marked_ignored()
    {
        // Given a podcast with default minimum duration and a discovered episode below that threshold
        var harness = new PodcastUpdaterTestHarness();
        var podcast = PodcastFixtures.SpotifyPrimary("show-short", Guid.Parse("99999999-9999-9999-9999-999999999999"));
        harness.PodcastRepository.Seed(podcast);

        var shortEpisode = EpisodeFixtures.FromSpotifyCatalogue(
            "short-spot-1",
            "Short episode",
            new Uri("https://open.spotify.com/episode/short-spot-1"),
            EpisodeRelease,
            TimeSpan.FromMinutes(2),
            "Too short");

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([shortEpisode]);

        // When full indexing runs without removing short episodes before merge
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Then the short episode is added with Ignored set before persistence
        harness.EpisodeRepository.SavedEpisodes.Should().ContainSingle();
        var saved = harness.EpisodeRepository.SavedEpisodes.Single();
        saved.Id.Should().Be(shortEpisode.Id);
        saved.Ignored.Should().BeTrue();
    }
}
