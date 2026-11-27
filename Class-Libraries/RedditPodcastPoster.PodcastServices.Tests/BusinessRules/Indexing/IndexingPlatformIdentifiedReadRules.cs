using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
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
    // Distinct calendar days + far-apart durations: same-day release+duration heuristics must not also match the
    // YouTube-only row when a discovered sibling arrives (that failed the merge as ambiguous and flaked CI).
    private static readonly DateTime AppleOutsideWindowRelease = DomainTestFixture.UtcDateDaysAgo(520);
    private static readonly DateTime SpotifyOutsideWindowRelease = DomainTestFixture.UtcDateDaysAgo(510);
    private static readonly DateTime YouTubeOutsideWindowRelease = DomainTestFixture.UtcDateDaysAgo(500);
    private static readonly DateTime SpotifyUrlOutsideWindowRelease = DomainTestFixture.UtcDateDaysAgo(490);
    private static readonly TimeSpan AppleOutsideWindowLength = TimeSpan.FromMinutes(41);
    private static readonly TimeSpan SpotifyOutsideWindowLength = TimeSpan.FromMinutes(53);
    private static readonly TimeSpan YouTubeOutsideWindowLength = TimeSpan.FromMinutes(67);
    private static readonly TimeSpan SpotifyUrlOutsideWindowLength = TimeSpan.FromMinutes(79);

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

        _appleOnly = _fixture.CreateAppleCatalogueEpisode(b => b
            .WithRelease(AppleOutsideWindowRelease)
            .WithDuration(AppleOutsideWindowLength));
        _spotifyOnly = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(SpotifyOutsideWindowRelease)
            .WithDuration(SpotifyOutsideWindowLength));
        _youTubeOnly = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithRelease(YouTubeOutsideWindowRelease)
            .WithDuration(YouTubeOutsideWindowLength));
        _spotifyUrlOnly = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(SpotifyUrlOutsideWindowRelease)
            .WithDuration(SpotifyUrlOutsideWindowLength));
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

    [Fact(DisplayName =
        "When discovery merge fills Spotify onto a YouTube-only outside-window episode, that episode stays out of the " +
        "enrichment context, because Apple/Spotify membership is the pre-merge Cosmos set (same as the old second read).")]
    public async Task discovery_enrichment_context_excludes_youtube_only_episode_after_merge_fills_spotify()
    {
        // Arrange
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(_youTubeOnly)!;
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeId)
            .WithTitle(_youTubeOnly.Title)
            .WithDuration(_youTubeOnly.Length)
            .WithRelease(_youTubeOnly.ReleaseUtc));
        EpisodeServicePresence.SetSpotifyIdentity(discovered, spotifyInput.SpotifyId);
        EpisodeServicePresence.Upsert(discovered, ServiceKeys.Spotify, spotifyInput.SpotifyUrl, null);

        _harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(_podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        var context = PodcastUpdaterTestHarness.DefaultIndexingContext();

        // Act
        var result = await _harness.Updater.Update(_podcast, enrichOnly: false, context);

        // Assert
        var mergedExisting = result.MergeResult.MergedEpisodes
            .Should().ContainSingle(m => m.Existing.Id == _youTubeOnly.Id).Subject.Existing;
        EpisodeServicePresence.SpotifyEpisodeId(mergedExisting).Should().Be(spotifyInput.SpotifyId,
            "merge must have filled Spotify onto the shared YouTube-only ref (otherwise this test does not probe widening)");
        _enrichmentContext.Select(x => x.Id).Should().BeEquivalentTo([_appleOnly.Id, _spotifyOnly.Id]);
        _enrichmentContext.Select(x => x.Id).Should().NotContain(_youTubeOnly.Id);
    }
}
