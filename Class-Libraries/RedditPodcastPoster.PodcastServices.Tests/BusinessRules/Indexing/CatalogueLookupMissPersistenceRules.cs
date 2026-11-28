using FluentAssertions;
using Moq;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// Index miss memory is stored on the podcast with the existing save: out-of-scope entries are pruned, and there is
/// no second Cosmos write (#1558).
/// </summary>
public class CatalogueLookupMissPersistenceRules
{
    private readonly DomainTestFixture _fixture = new();
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(14);
    private static readonly DateTime InWindowRelease = DomainTestFixture.UtcDateDaysAgo(3);

    [Fact(DisplayName =
        "Remembered misses for episodes that left the indexing window are removed when the podcast is saved.")]
    public async Task Out_of_scope_misses_are_pruned()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var inScope = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: InWindowRelease);
        var outOfScopeId = _fixture.CreateGuid();
        podcast.LookupMisses =
        [
            new EpisodeLookupMiss
            {
                EpisodeId = inScope.Id,
                Platform = CataloguePlatforms.Spotify,
                NewestCatalogueId = _fixture.CreateSpotifyId(),
                ShowTotal = 10,
                LastFullAttemptUtc = DomainTestFixture.UtcDaysAgo(0)
            },
            new EpisodeLookupMiss
            {
                EpisodeId = outOfScopeId,
                Platform = CataloguePlatforms.Spotify,
                NewestCatalogueId = _fixture.CreateSpotifyId(),
                ShowTotal = 10,
                LastFullAttemptUtc = DomainTestFixture.UtcDaysAgo(1)
            }
        ];
        harness.PodcastRepository.Seed(podcast);
        harness.EpisodeRepository.Seed(inScope);
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast, enrichOnly: false, PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        var saved = harness.PodcastRepository.SavedPodcasts.Should().ContainSingle().Subject;
        saved.LookupMisses.Should().NotBeNull();
        saved.LookupMisses!.Should().ContainSingle(m => m.EpisodeId == inScope.Id);
        saved.LookupMisses.Should().NotContain(m => m.EpisodeId == outOfScopeId);
    }

    [Fact(DisplayName =
        "Remembered misses are saved with the podcast's existing save, not a second write.")]
    public async Task Misses_use_existing_podcast_save()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: InWindowRelease);
        harness.PodcastRepository.Seed(podcast);
        harness.EpisodeRepository.Seed(episode);
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([]);
        harness.LookupMisses.RecordMiss(
            podcast.Id,
            episode.Id,
            CataloguePlatforms.Spotify,
            _fixture.CreateSpotifyId(),
            12,
            DomainTestFixture.UtcDaysAgo(0));

        // Act
        await harness.Updater.Update(
            podcast, enrichOnly: false, PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.PodcastRepository.SavedPodcasts.Should().ContainSingle();
        var saved = harness.PodcastRepository.SavedPodcasts[0];
        saved.LookupMisses.Should().ContainSingle(m =>
            m.EpisodeId == episode.Id && m.Platform == CataloguePlatforms.Spotify && m.ShowTotal == 12);
    }
}
