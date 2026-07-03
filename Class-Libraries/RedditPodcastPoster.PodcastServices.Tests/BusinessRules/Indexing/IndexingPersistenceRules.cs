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
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private static readonly DateTime EpisodeRelease = DomainTestFixture.UtcDateDaysAgo(30);
    private static readonly TimeSpan SubMinimumDuration =
        PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration - TimeSpan.FromMinutes(1);

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Persist order: enriched episodes are saved, then filtered, then merged existing, then added.")]
    public async Task persist_order_is_enriched_then_filtered_then_merged_then_added()
    {
        // Arrange
        var recorder = new SaveCallRecorder();
        var harness = new PodcastUpdaterTestHarness(recorder);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId(), Guid.Parse("11111111-1111-1111-1111-111111111111"));
        harness.PodcastRepository.Seed(podcast);

        var enrichedEpisodeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var filteredEpisodeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var mergedExistingId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var enrichedInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithRelease(EpisodeRelease.AddDays(1))
            .WithDuration(_fixture.CreateDuration())
            .WithDescription("Enriched description"));
        var filteredInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithRelease(EpisodeRelease.AddDays(2))
            .WithDuration(_fixture.CreateDuration())
            .WithDescription("Filtered description"));
        var mergeInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration())
            .WithDescription("Truncated catalogue desc..."));
        var addedInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithRelease(EpisodeRelease.AddDays(3))
            .WithDuration(_fixture.CreateDuration())
            .WithDescription("Brand new episode"));

        var enrichedEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(enrichedInput.SpotifyId)
            .WithRelease(enrichedInput.Release)
            .WithDuration(enrichedInput.Duration)
            .WithDescription(enrichedInput.Description));
        enrichedEpisode.Id = enrichedEpisodeId;
        enrichedEpisode.PodcastId = podcast.Id;

        var filteredEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(filteredInput.SpotifyId)
            .WithRelease(filteredInput.Release)
            .WithDuration(filteredInput.Duration)
            .WithDescription(filteredInput.Description));
        filteredEpisode.Id = filteredEpisodeId;
        filteredEpisode.PodcastId = podcast.Id;

        var mergedExisting = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(mergeInput.SpotifyId)
            .WithRelease(mergeInput.Release)
            .WithDuration(mergeInput.Duration)
            .WithDescription(mergeInput.Description));
        mergedExisting.Id = mergedExistingId;
        mergedExisting.PodcastId = podcast.Id;

        harness.EpisodeRepository.Seed(enrichedEpisode, filteredEpisode, mergedExisting);

        var mergeDiscovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(mergeInput.SpotifyId)
            .WithSpotifyUrl(new Uri($"{mergeInput.SpotifyUrl}?si=discovered"))
            .WithRelease(mergeInput.Release)
            .WithDuration(mergeInput.Duration)
            .WithDescription("Longer merged description from catalogue"));

        var addedDiscovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(addedInput.SpotifyId)
            .WithRelease(addedInput.Release)
            .WithDuration(addedInput.Duration)
            .WithDescription(addedInput.Description));

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
                    new EnrichmentContext { Spotify = new Uri($"{enrichedInput.SpotifyUrl}?si=enriched") })
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
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId(), Guid.Parse("55555555-5555-5555-5555-555555555555"));
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
        podcast.SpotifyId = _fixture.CreateSpotifyId();
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        var sharedLength = _fixture.CreateDuration();
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
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId(), Guid.Parse("77777777-7777-7777-7777-777777777777"));
        harness.PodcastRepository.Seed(podcast);

        var stored = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration()));
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
