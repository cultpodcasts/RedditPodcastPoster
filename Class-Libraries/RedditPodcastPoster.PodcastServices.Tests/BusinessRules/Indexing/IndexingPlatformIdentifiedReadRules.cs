using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// The episodes that carry an Apple or Spotify id feed the enrichment context. Discovery already reads every
/// platform-identified episode for the merge, so it must not read them a second time.
/// </summary>
public class IndexingPlatformIdentifiedReadRules
{
    // Older than the indexing window, so these episodes only reach the updater through the platform-id reads.
    private static readonly DateTime OutsideWindowRelease = DomainTestFixture.UtcDateDaysAgo(500);

    private readonly DomainTestFixture _fixture = new();
    private readonly PodcastUpdaterTestHarness _harness = new();
    private readonly Podcast _podcast;
    private readonly List<Episode> _enrichmentContext = [];
    private readonly Episode _appleOnly;
    private readonly Episode _spotifyOnly;
    private readonly Episode _youTubeOnly;
    private readonly Episode _spotifyUrlOnly;

    public IndexingPlatformIdentifiedReadRules()
    {
        _podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _harness.PodcastRepository.Seed(_podcast);

        _appleOnly = _fixture.CreateAppleCatalogueEpisode(b => b.WithRelease(OutsideWindowRelease));
        _spotifyOnly = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithRelease(OutsideWindowRelease));
        _youTubeOnly = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(OutsideWindowRelease));
        _spotifyUrlOnly = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithRelease(OutsideWindowRelease));
        EpisodeServicePresence.SetSpotifyIdentity(_spotifyUrlOnly, null);

        foreach (var episode in new[] { _appleOnly, _spotifyOnly, _youTubeOnly, _spotifyUrlOnly })
        {
            episode.PodcastId = _podcast.Id;
        }

        _harness.EpisodeRepository.Seed(_appleOnly, _spotifyOnly, _youTubeOnly, _spotifyUrlOnly);

        _harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(_podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);
        _harness.EpisodeEnricher
            .Setup(x => x.EnrichEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IList<Episode>, IndexingContext>((_, context, _, _) =>
            {
                _enrichmentContext.Clear();
                _enrichmentContext.AddRange(context);
            })
            .ReturnsAsync(new EnrichmentResults([]));
    }

    [Fact(DisplayName =
        "When discovery runs, the episodes with platform ids are read from Cosmos once per podcast, " +
        "because the merge read already loaded them.")]
    public async Task discovery_reads_platform_identified_episodes_once()
    {
        // Arrange
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext();

        // Act
        await _harness.Updater.Update(_podcast, enrichOnly: false, context);

        // Assert
        _harness.EpisodeRepository.CountPodcastSelectorQueriesSelecting(_appleOnly).Should().Be(1);
    }

    [Fact(DisplayName =
        "When discovery runs, the enrichment context is the episodes with an Apple or Spotify id and not the " +
        "YouTube-only or Spotify-url-only episodes, the same set as the separate read returned.")]
    public async Task discovery_enrichment_context_holds_only_apple_or_spotify_identified_episodes()
    {
        // Arrange
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext();

        // Act
        await _harness.Updater.Update(_podcast, enrichOnly: false, context);

        // Assert
        _enrichmentContext.Select(x => x.Id).Should().BeEquivalentTo([_appleOnly.Id, _spotifyOnly.Id]);
        _enrichmentContext.Select(x => x.Id).Should().NotContain([_youTubeOnly.Id, _spotifyUrlOnly.Id]);
    }

    [Fact(DisplayName =
        "When discovery is skipped, the episodes with platform ids are still read from Cosmos and fed to enrichment, " +
        "because the enrich-only branch has no merge read to reuse.")]
    public async Task enrich_only_still_reads_platform_identified_episodes()
    {
        // Arrange
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext();

        // Act
        await _harness.Updater.Update(_podcast, enrichOnly: true, context);

        // Assert
        _harness.EpisodeRepository.CountPodcastSelectorQueriesSelecting(_appleOnly).Should().Be(1);
        _enrichmentContext.Select(x => x.Id).Should().BeEquivalentTo([_appleOnly.Id, _spotifyOnly.Id]);
    }
}
