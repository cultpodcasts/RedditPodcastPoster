using FluentAssertions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Tests.Support;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class IndexingOrchestrationRules
{
    private static readonly DateTime ReleasedSince = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EpisodeRelease = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Full indexing discovers episodes, merges, enriches, filters, then persists.")]
    public async Task full_indexing_runs_discover_merge_enrich_filter_and_persist_stages()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH", Guid.Parse("12121212-1212-1212-1212-121212121212"));
        harness.PodcastRepository.Seed(podcast);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(TimeSpan.FromMinutes(45)));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert discovery, enrichment, and filtering all run before persistence
        harness.EpisodeProvider.Verify(
            x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()),
            Times.Once);
        harness.EpisodeEnricher.Verify(
            x => x.EnrichEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()),
            Times.Once);
        harness.PodcastFilter.Verify(
            x => x.Filter(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<List<string>>()),
            Times.Once);
        harness.EpisodeRepository.SavedEpisodes.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When SkipShortEpisodes is set, short discovered episodes are removed before merge.")]
    public async Task skip_short_episodes_removes_short_discovered_episodes_before_merge()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH", Guid.Parse("13131313-1313-1313-1313-131313131313"));
        harness.PodcastRepository.Seed(podcast);

        var shortEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(TimeSpan.FromMinutes(2)));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([shortEpisode]);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince) with
        {
            SkipShortEpisodes = true
        };

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            indexingContext);

        // Assert the short episode is not added because it was removed before merge
        harness.EpisodeRepository.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "LatestReleased on the podcast reflects the most recent release among added and merged episodes.")]
    public async Task latest_released_is_updated_from_added_and_merged_episodes()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH", Guid.Parse("14141414-1414-1414-1414-141414141414"));
        podcast.LatestReleased = EpisodeRelease.AddDays(-30);
        harness.PodcastRepository.Seed(podcast);

        const string mergeSpotifyId = "1UncRhHtmojlTq2mO0Gntz";
        var mergedExisting = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(mergeSpotifyId)
            .WithRelease(EpisodeRelease.AddDays(1))
            .WithDuration(TimeSpan.FromMinutes(45))
            .WithDescription("Truncated description..."));
        mergedExisting.Id = Guid.Parse("15151515-1515-1515-1515-151515151515");
        mergedExisting.PodcastId = podcast.Id;
        harness.EpisodeRepository.Seed(mergedExisting);

        var mergeDiscovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(mergeSpotifyId)
            .WithSpotifyUrl(new Uri($"https://open.spotify.com/episode/{mergeSpotifyId}?si=merge"))
            .WithRelease(EpisodeRelease.AddDays(1))
            .WithDuration(TimeSpan.FromMinutes(45))
            .WithDescription("Longer merged description from catalogue"));

        var addedDiscovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId("3vKvHj9mNoPqRsTuVwXyZ1")
            .WithRelease(EpisodeRelease.AddDays(5))
            .WithDuration(TimeSpan.FromMinutes(50)));

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync([mergeDiscovered, addedDiscovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert LatestReleased reflects the newest added episode release
        podcast.LatestReleased.Should().Be(EpisodeRelease.AddDays(5));
        harness.PodcastRepository.GetStored(podcast.Id).LatestReleased.Should().Be(EpisodeRelease.AddDays(5));
    }

    [Fact(DisplayName =
        "Expensive-query flags discovered during indexing are persisted on the podcast.")]
    public async Task expensive_query_flags_are_persisted_on_the_podcast()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH", Guid.Parse("16161616-1616-1616-1616-161616161616"));
        podcast.YouTubeChannelId = "channel-expensive";
        podcast.SpotifyEpisodesQueryIsExpensive = null;
        podcast.YouTubePlaylistQueryIsExpensive = null;
        harness.PodcastRepository.Seed(podcast);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((indexedPodcast, _, _) =>
            {
                indexedPodcast.SpotifyEpisodesQueryIsExpensive = true;
                indexedPodcast.YouTubePlaylistQueryIsExpensive = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert the discovered expensive-query flags are persisted on the podcast
        podcast.HasExpensiveSpotifyEpisodesQuery().Should().BeTrue();
        podcast.HasExpensiveYouTubePlaylistQuery().Should().BeTrue();
        harness.PodcastRepository.SavedPodcasts.Should().ContainSingle();
        harness.PodcastRepository.GetStored(podcast.Id).HasExpensiveSpotifyEpisodesQuery().Should().BeTrue();
        harness.PodcastRepository.GetStored(podcast.Id).HasExpensiveYouTubePlaylistQuery().Should().BeTrue();
    }

    [Fact(DisplayName =
        "LastIndexed is not updated when Spotify URL resolving is bypassed during indexing.")]
    public async Task last_indexed_is_not_set_when_spotify_bypass_occurs()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH", Guid.Parse("17171717-1717-1717-1717-171717171717"));
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((_, _, context) =>
            {
                context.SkipSpotifyUrlResolving = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            indexingContext);

        // Assert LastIndexed remains unset
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "LastIndexed is not updated when YouTube URL resolving is bypassed during indexing.")]
    public async Task last_indexed_is_not_set_when_youtube_bypass_occurs()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH", Guid.Parse("18181818-1818-1818-1818-181818181818"));
        podcast.YouTubeChannelId = "channel-youtube-bypass";
        podcast.LastIndexed = null;
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince);

        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(
                podcast,
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .Callback<Podcast, IEnumerable<Episode>, IndexingContext>((_, _, context) =>
            {
                context.SkipYouTubeUrlResolving = true;
            })
            .ReturnsAsync([]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            indexingContext);

        // Assert LastIndexed remains unset
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "LastIndexed is not updated when scheduled YouTube discovery is bypassed.")]
    public async Task last_indexed_is_not_set_when_scheduled_youtube_discovery_is_bypassed()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = new Podcast
        {
            Id = Guid.Parse("19191919-1919-1919-1919-191919191919"),
            Name = "Channel-only podcast",
            YouTubeChannelId = "channel-only-bypass",
            LastIndexed = null
        };
        harness.PodcastRepository.Seed(podcast);

        var indexingContext = new IndexingContext(ReleasedSince, SkipYouTubeUrlResolving: true);

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
            indexingContext);

        // Assert LastIndexed remains unset
        podcast.LastIndexed.Should().BeNull();
        harness.PodcastRepository.SavedPodcasts.Should().BeEmpty();
    }
}
