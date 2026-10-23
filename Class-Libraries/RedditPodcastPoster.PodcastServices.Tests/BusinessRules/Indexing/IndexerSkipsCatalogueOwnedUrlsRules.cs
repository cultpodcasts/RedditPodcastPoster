using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// After a podcast episode is moved to a film, news report, or TV episode, the feed item can still
/// sit inside the index window. The indexer must not save it again under a new episode id.
/// </summary>
public class IndexerSkipsCatalogueOwnedUrlsRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private static readonly DateTime EpisodeRelease = DomainTestFixture.UtcDateDaysAgo(30);

    private readonly DomainTestFixture _fixture = new();

    public static TheoryData<string, string> OwnedServiceAndKind() =>
        new()
        {
            { ServiceKeys.YouTube, SubmitClassification.Film },
            { ServiceKeys.Spotify, SubmitClassification.TvShowEpisode },
            { ServiceKeys.Apple, SubmitClassification.NewsReport }
        };

    [Theory(DisplayName =
        "When a newly found episode's YouTube, Spotify, or Apple URL is already stored on a film, TV episode, or news report, the indexer does not save that episode and logs the stable skip prefix, because the feed item was moved out of the podcast and must not be re-added.")]
    [MemberData(nameof(OwnedServiceAndKind))]
    public async Task newly_found_episode_with_a_catalogue_owned_service_url_is_not_saved(
        string serviceKey,
        string contentKind)
    {
        // Arrange
        var logger = new CapturedInformationLogger();
        var harness = new PodcastUpdaterTestHarness(logger: logger);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        harness.PodcastRepository.Seed(podcast);
        var discovered = CreateDiscoveredEpisode(serviceKey);
        var ownedUrl = EpisodeServicePresence.TryGetUrl(discovered, serviceKey)!;
        harness.UrlMembershipLookup
            .Setup(x => x.FindCatalogueServiceUrlOwner(ownedUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner(contentKind, ParentName: null));
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.EpisodeRepository.SavedEpisodes.Should().NotContain(episode => episode.Id == discovered.Id);
        harness.EpisodeEnricher.Verify(
            x => x.EnrichEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.Is<IList<Episode>>(episodes => episodes.All(episode => episode.Id != discovered.Id)),
                It.IsAny<IndexingContext>()),
            Times.Once);
        var skip = logger.Messages.Should().ContainSingle(message =>
            message.StartsWith(PodcastUpdater.CatalogueOwnedUrlSkippedMessagePrefix, StringComparison.Ordinal))
            .Which;
        skip.Should().Contain(contentKind);
        skip.Should().Contain(discovered.Id.ToString());
        PodcastUpdater.CatalogueOwnedUrlSkippedMessagePrefix.Should().Be("Indexer skipped catalogue-owned URL:");
    }

    [Fact(DisplayName =
        "When one newly found episode's service URL is catalogue-owned and another is not, the indexer saves only the unowned episode, because the ownership check applies per new item.")]
    public async Task only_the_unowned_new_episode_is_saved()
    {
        // Arrange
        var logger = new CapturedInformationLogger();
        var harness = new PodcastUpdaterTestHarness(logger: logger);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        harness.PodcastRepository.Seed(podcast);
        var owned = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration()));
        var free = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration()));
        var ownedUrl = EpisodeServicePresence.TryGetUrl(owned, ServiceKeys.Spotify)!;
        harness.UrlMembershipLookup
            .Setup(x => x.FindCatalogueServiceUrlOwner(ownedUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner(SubmitClassification.Film, ParentName: null));
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([owned, free]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.EpisodeRepository.SavedEpisodes.Select(episode => episode.Id)
            .Should().ContainSingle().Which.Should().Be(free.Id);
        logger.Messages.Should().ContainSingle(message =>
                message.StartsWith(PodcastUpdater.CatalogueOwnedUrlSkippedMessagePrefix, StringComparison.Ordinal))
            .Which.Should().Contain(owned.Id.ToString());
    }

    [Fact(DisplayName =
        "When a newly found episode's service URL is not stored on a film, TV episode, or news report, the indexer still saves it, because catalogue ownership is the only reason to drop a new feed item.")]
    public async Task newly_found_episode_with_an_unowned_service_url_is_saved()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        harness.PodcastRepository.Seed(podcast);
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithRelease(EpisodeRelease)
            .WithDuration(_fixture.CreateDuration()));
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.EpisodeRepository.SavedEpisodes.Select(episode => episode.Id)
            .Should().Contain(discovered.Id);
        harness.UrlMembershipLookup.Verify(
            x => x.FindCatalogueServiceUrlOwner(
                EpisodeServicePresence.TryGetUrl(discovered, ServiceKeys.Spotify)!,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When the feed item matches an episode already stored on the podcast, the indexer does not consult catalogue URL ownership, because the check is only for newly found items.")]
    public async Task existing_episode_matched_from_the_feed_is_not_treated_as_catalogue_owned()
    {
        // Arrange
        var harness = new PodcastUpdaterTestHarness();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        harness.PodcastRepository.Seed(podcast);
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: EpisodeRelease);
        harness.EpisodeRepository.Seed(stored);
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored)!;
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyId)
            .WithSpotifyUrl(EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify)!)
            .WithRelease(EpisodeRelease)
            .WithDuration(stored.Length)
            .WithTitle(stored.Title));
        harness.EpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync([discovered]);

        // Act
        await harness.Updater.Update(
            podcast,
            enrichOnly: false,
            PodcastUpdaterTestHarness.DefaultIndexingContext(ReleasedSince));

        // Assert
        harness.UrlMembershipLookup.Verify(
            x => x.FindCatalogueServiceUrlOwner(It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.EpisodeRepository.SavedEpisodes.Select(episode => episode.Id)
            .Should().OnlyContain(id => id == stored.Id);
    }

    private Episode CreateDiscoveredEpisode(string serviceKey) =>
        serviceKey switch
        {
            ServiceKeys.YouTube => _fixture.CreateYouTubeCatalogueEpisode(b => b
                .WithRelease(EpisodeRelease)
                .WithDuration(_fixture.CreateDuration())),
            ServiceKeys.Spotify => _fixture.CreateSpotifyCatalogueEpisode(b => b
                .WithRelease(EpisodeRelease)
                .WithDuration(_fixture.CreateDuration())),
            ServiceKeys.Apple => _fixture.CreateAppleCatalogueEpisode(b => b
                .WithRelease(EpisodeRelease)
                .WithDuration(_fixture.CreateDuration())),
            _ => throw new ArgumentOutOfRangeException(nameof(serviceKey), serviceKey, null)
        };

    private sealed class CapturedInformationLogger : ILogger<PodcastUpdater>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                Messages.Add(formatter(state, exception));
            }
        }
    }
}
