using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class IndexingPersistenceRules
{
    private static readonly DateTime ReleasedSince = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EpisodeRelease = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Persist order: enriched episodes are saved, then filtered, then merged existing, then added.")]
    public async Task persist_order_is_enriched_then_filtered_then_merged_then_added()
    {
        // Arrange
        var recorder = new SaveCallRecorder();
        var harness = new PodcastUpdaterTestHarness(recorder);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("show-save-order", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        harness.PodcastRepository.Seed(podcast);

        var enrichedEpisodeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var filteredEpisodeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var mergedExistingId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        const string mergeSpotifyId = "merge-spot-1";
        const string addedSpotifyId = "brand-new-99";

        var enrichedEpisode = _fixture.CreateSpotifyCatalogueEpisode(
            "enriched-spot-1",
            "Enriched episode",
            new Uri("https://open.spotify.com/episode/enriched-spot-1"),
            EpisodeRelease.AddDays(1),
            TimeSpan.FromMinutes(45),
            "Enriched description");
        enrichedEpisode.Id = enrichedEpisodeId;
        enrichedEpisode.PodcastId = podcast.Id;

        var filteredEpisode = _fixture.CreateSpotifyCatalogueEpisode(
            "filtered-spot-1",
            "Filtered episode",
            new Uri("https://open.spotify.com/episode/filtered-spot-1"),
            EpisodeRelease.AddDays(2),
            TimeSpan.FromMinutes(45),
            "Filtered description");
        filteredEpisode.Id = filteredEpisodeId;
        filteredEpisode.PodcastId = podcast.Id;

        var mergedExisting = _fixture.CreateSpotifyCatalogueEpisode(
            mergeSpotifyId,
            "Merged episode",
            new Uri($"https://open.spotify.com/episode/{mergeSpotifyId}"),
            EpisodeRelease,
            TimeSpan.FromMinutes(45),
            "Truncated catalogue desc...");
        mergedExisting.Id = mergedExistingId;
        mergedExisting.PodcastId = podcast.Id;

        harness.EpisodeRepository.Seed(enrichedEpisode, filteredEpisode, mergedExisting);

        var mergeDiscovered = _fixture.CreateSpotifyCatalogueEpisode(
            mergeSpotifyId,
            "Merged episode",
            new Uri($"https://open.spotify.com/episode/{mergeSpotifyId}?si=discovered"),
            EpisodeRelease,
            TimeSpan.FromMinutes(45),
            "Longer merged description from catalogue");

        var addedDiscovered = _fixture.CreateSpotifyCatalogueEpisode(
            addedSpotifyId,
            "Added episode",
            new Uri($"https://open.spotify.com/episode/{addedSpotifyId}"),
            EpisodeRelease.AddDays(3),
            TimeSpan.FromMinutes(50),
            "Brand new episode");

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([mergeDiscovered, addedDiscovered]);

        harness.EpisodeEnricher
            .Setup(x => x.EnrichEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EnrichmentResults([
                new EnrichmentResult(
                    podcast,
                    enrichedEpisode,
                    new EnrichmentContext { Spotify = new Uri("https://open.spotify.com/episode/enriched-spot-1?si=enriched") })
            ]));

        harness.PodcastFilter
            .Setup(x => x.Filter(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<List<string>>()))
            .Returns(new FilterResult([new FilteredEpisode(filteredEpisode, ["term"])]))
            ;

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        recorder.EpisodeCalls.Should().HaveCount(4);
        recorder.EpisodeCalls[0].EpisodeIds.Should().ContainSingle().Which.Should().Be(enrichedEpisodeId);
        recorder.EpisodeCalls[1].EpisodeIds.Should().ContainSingle().Which.Should().Be(filteredEpisodeId);
        recorder.EpisodeCalls[2].EpisodeIds.Should().ContainSingle().Which.Should().Be(mergedExistingId);
        recorder.EpisodeCalls[3].EpisodeIds.Should().ContainSingle().Which.Should().Be(addedDiscovered.Id);
    }

    [Fact(DisplayName =
        "LastIndexed is updated only when indexing succeeds without merge failures or platform bypasses.")]
    public async Task last_indexed_is_set_on_successful_index()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("show-success", Guid.Parse("55555555-5555-5555-5555-555555555555"));
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert LastIndexed is set on the podcast
        podcast.LastIndexed.Should().NotBeNull();
        harness.PodcastRepository.SavedPodcasts.Should().ContainSingle();
        harness.PodcastRepository.GetStored(podcast.Id).LastIndexed.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "LastIndexed is not updated when indexing records merge failures.")]
    public async Task last_indexed_is_not_set_when_merge_failures_occur()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreatePodcast(p => p.Id = Guid.Parse("66666666-6666-6666-6666-666666666666"));
        podcast.SpotifyId = "show-ambiguous";
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        var sharedLength = TimeSpan.FromMinutes(45);
        var (youTubeOnly, appleOnly) = _fixture.CreateAmbiguousMatchStoredEpisodes(
            podcast,
            EpisodeRelease,
            sharedLength);
        harness.EpisodeRepository.Seed(youTubeOnly, appleOnly);

        var discovered = _fixture.CreateAmbiguousMatchSpotifyIncoming(EpisodeRelease, sharedLength);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        var result = await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.MergeResult.FailedEpisodes.Should().ContainSingle();
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Enrich-only indexing does not discover new catalogue episodes from the episode provider.")]
    public async Task enrich_only_does_not_call_episode_provider_for_discovery()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("show-enrich-only", Guid.Parse("77777777-7777-7777-7777-777777777777"));
        harness.PodcastRepository.Seed(podcast);

        var stored = _fixture.CreateYouTubeCatalogueEpisode(
            "yt-only-1",
            "YouTube-only stored episode",
            EpisodeRelease,
            TimeSpan.FromMinutes(45));
        stored.Id = Guid.Parse("88888888-8888-8888-8888-888888888888");
        stored.PodcastId = podcast.Id;
        stored.SpotifyId = string.Empty;
        stored.Urls.Spotify = null;
        harness.EpisodeRepository.Seed(stored);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: true,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert the episode provider is never asked for new catalogue episodes
        harness.EpisodeProvider.Verify(
            x => x.GetEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
    }
}
