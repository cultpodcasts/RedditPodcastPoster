using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// The backlog index confirmation (#1433) asks the updater how many episodes a backlog index would ingest, without
/// ingesting them. The preview shares the discovery and merge pass with the real update, so these rules pin what the
/// count includes and that the dry run writes nothing.
/// </summary>
public class PodcastUpdaterPreviewRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private static readonly DateTime EpisodeRelease = DomainTestFixture.UtcDateDaysAgo(30);
    private static readonly TimeSpan MinimumDuration = PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration;

    private readonly DomainTestFixture _fixture = new();
    private readonly PodcastUpdaterTestHarness _harness = new();

    [Fact(DisplayName =
        "Previewing a backlog index counts every newly found episode and saves no episode or podcast, because the confirmation must report what an index would ingest without ingesting it.")]
    public async Task preview_counts_new_episodes_and_saves_nothing()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _harness.PodcastRepository.Seed(podcast);
        var first = CreateDiscoveredEpisode();
        var second = CreateDiscoveredEpisode();
        ProviderFinds(podcast, first, second);

        // Act
        var result = await _harness.Updater.Preview(
            podcast,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.NewEpisodeCount.Should().Be(2);
        result.IgnoredEpisodeCount.Should().Be(0);
        _harness.EpisodeRepository.SavedEpisodes.Should().BeEmpty();
        _harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
        _harness.EpisodeEnricher.Verify(
            x => x.EnrichEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Previewing a backlog index does not count a feed item that matches an episode already stored on the podcast, because only episodes not yet in the catalogue would be ingested.")]
    public async Task preview_does_not_count_an_episode_already_in_the_catalogue()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _harness.PodcastRepository.Seed(podcast);
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: EpisodeRelease);
        _harness.EpisodeRepository.Seed(stored);
        var alreadyStored = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(EpisodeServicePresence.SpotifyEpisodeId(stored)!)
            .WithSpotifyUrl(EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify)!)
            .WithRelease(EpisodeRelease)
            .WithDuration(stored.Length)
            .WithTitle(stored.Title));
        var fresh = CreateDiscoveredEpisode();
        ProviderFinds(podcast, alreadyStored, fresh);

        // Act
        var result = await _harness.Updater.Preview(
            podcast,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.NewEpisodeCount.Should().Be(1);
    }

    [Fact(DisplayName =
        "Previewing a backlog index does not count an episode shorter than the minimum duration when short episodes are skipped, because the real index drops it.")]
    public async Task preview_excludes_short_episodes_when_short_episodes_are_skipped()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _harness.PodcastRepository.Seed(podcast);
        var shortEpisode = CreateDiscoveredEpisode(MinimumDuration - TimeSpan.FromSeconds(1));
        var longEnough = CreateDiscoveredEpisode(MinimumDuration);
        ProviderFinds(podcast, shortEpisode, longEnough);
        var context = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince) with { SkipShortEpisodes = true };

        // Act
        var result = await _harness.Updater.Preview(podcast, context);

        // Assert
        result.NewEpisodeCount.Should().Be(1);
        result.IgnoredEpisodeCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Previewing a backlog index does not count a newly found episode whose service URL is stored on a film, TV episode or news report, because the real index drops catalogue-owned URLs.")]
    public async Task preview_excludes_catalogue_owned_urls()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _harness.PodcastRepository.Seed(podcast);
        var owned = CreateDiscoveredEpisode();
        var free = CreateDiscoveredEpisode();
        _harness.UrlMembershipLookup
            .Setup(x => x.FindCatalogueServiceUrlOwner(
                EpisodeServicePresence.TryGetUrl(owned, ServiceKeys.Spotify)!,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner(SubmitClassification.Film, ParentName: null));
        ProviderFinds(podcast, owned, free);

        // Act
        var result = await _harness.Updater.Preview(
            podcast,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.NewEpisodeCount.Should().Be(1);
    }

    [Fact(DisplayName =
        "Previewing a backlog index for a podcast that ignores all episodes counts them as new and ignored, because the curator must see that the episodes will be ingested as ignored.")]
    public async Task preview_reports_ignored_episodes_for_an_ignore_all_podcast()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IgnoreAllEpisodes = true;
        _harness.PodcastRepository.Seed(podcast);
        ProviderFinds(podcast, CreateDiscoveredEpisode(), CreateDiscoveredEpisode());

        // Act
        var result = await _harness.Updater.Preview(
            podcast,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.NewEpisodeCount.Should().Be(2);
        result.IgnoredEpisodeCount.Should().Be(2);
    }

    [Fact(DisplayName =
        "Previewing a backlog index does not count an episode the elimination-terms filter removes, because the real index removes it before saving.")]
    public async Task preview_excludes_episodes_removed_by_the_elimination_terms_filter()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _harness.PodcastRepository.Seed(podcast);
        var eliminated = CreateDiscoveredEpisode();
        var kept = CreateDiscoveredEpisode();
        ProviderFinds(podcast, eliminated, kept);
        _harness.PodcastFilter
            .Setup(x => x.Filter(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<List<string>>()))
            .Returns(new FilterResult([new FilteredEpisode(eliminated, [_fixture.Create<string>()])]));

        // Act
        var result = await _harness.Updater.Preview(
            podcast,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        result.NewEpisodeCount.Should().Be(1);
    }

    private void ProviderFinds(Podcast podcast, params Episode[] discovered) =>
        _harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(discovered.ToList());

    private Episode CreateDiscoveredEpisode(TimeSpan? duration = null) =>
        _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(duration ?? MinimumDuration + _fixture.CreateDuration()));
}
