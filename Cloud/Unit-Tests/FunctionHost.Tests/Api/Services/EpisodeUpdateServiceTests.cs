using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using Azure.Search.Documents;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Reddit.Managers;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services;

public class EpisodeUpdateServiceTests
{
    [Fact(DisplayName =
        "Plain English rule: when the episode is not found, then return NotFound and do not save, because there is nothing to update.")]
    public async Task update_returns_not_found_and_does_not_save_when_episode_missing()
    {
        // Arrange
        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastNotFound));

        var episodeRepo = new Mock<IEpisodeRepository>(MockBehavior.Strict);
        var service = CreateService(resolver.Object, episodeRepo.Object);

        // Act
        var result = await service.UpdateAsync(
            new EpisodeChangeRequestWrapper(Guid.NewGuid(), Guid.NewGuid(), new EpisodeChangeRequest { Title = "New" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.NotFound);
        episodeRepo.Verify(r => r.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when the podcast is not found for a resolved episode, then return NotFound and do not save, because the episode cannot be updated without its podcast.")]
    public async Task update_returns_not_found_and_does_not_save_when_podcast_missing()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode { Id = episodeId, PodcastId = podcastId, Release = DateTime.UtcNow.AddDays(-30) };

        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, null, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = new Mock<IEpisodeRepository>(MockBehavior.Strict);
        var service = CreateService(resolver.Object, episodeRepo.Object);

        // Act
        var result = await service.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcastId, episodeId, new EpisodeChangeRequest { Title = "New" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.NotFound);
        episodeRepo.Verify(r => r.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when a title-only change is accepted, then save the episode once, because persistence is the core update outcome.")]
    public async Task update_happy_path_saves_episode_once()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode
        {
            Id = episodeId,
            PodcastId = podcastId,
            Title = "Original",
            Release = DateTime.UtcNow.AddDays(-30)
        };
        var podcast = new Podcast { Id = podcastId, Name = "Show" };

        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = new Mock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.Save(It.IsAny<Episode>())).Returns(Task.CompletedTask);

        var indexer = new Mock<IEpisodeSearchIndexerService>();
        indexer.Setup(s => s.IndexEpisode(It.IsAny<Podcast>(), It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

        var service = CreateService(resolver.Object, episodeRepo.Object, indexer: indexer.Object);

        // Act
        var result = await service.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcastId, episodeId, new EpisodeChangeRequest { Title = "Updated" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        episodeRepo.Verify(r => r.Save(episode), Times.Once);
    }

    [Fact(DisplayName =
        "Plain English rule: when the change request does not untweet or unbluesky, then social remove managers are not called, because those side effects are flag-driven.")]
    public async Task update_without_unsocial_flags_does_not_call_social_managers()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode
        {
            Id = episodeId,
            PodcastId = podcastId,
            Title = "Original",
            Tweeted = true,
            BlueskyPosted = true,
            Release = DateTime.UtcNow.AddDays(-30)
        };
        var podcast = new Podcast { Id = podcastId, Name = "Show" };

        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = new Mock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.Save(It.IsAny<Episode>())).Returns(Task.CompletedTask);

        var tweetManager = new Mock<ITweetManager>(MockBehavior.Strict);
        var blueskyPostManager = new Mock<IBlueskyPostManager>(MockBehavior.Strict);

        var indexer = new Mock<IEpisodeSearchIndexerService>();
        indexer.Setup(s => s.IndexEpisode(It.IsAny<Podcast>(), It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

        var service = CreateService(
            resolver.Object,
            episodeRepo.Object,
            tweetManager: tweetManager.Object,
            blueskyPostManager: blueskyPostManager.Object,
            indexer: indexer.Object);

        // Act
        var result = await service.UpdateAsync(
            new EpisodeChangeRequestWrapper(podcastId, episodeId, new EpisodeChangeRequest { Title = "Updated" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        tweetManager.Verify(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()), Times.Never);
        blueskyPostManager.Verify(m => m.RemovePost(It.IsAny<PodcastEpisode>()), Times.Never);
    }

    private static EpisodeUpdateService CreateService(
        IPodcastEpisodeResolver resolver,
        IEpisodeRepository episodeRepository,
        ITweetManager? tweetManager = null,
        IBlueskyPostManager? blueskyPostManager = null,
        IEpisodeSearchIndexerService? indexer = null)
    {
        return new EpisodeUpdateService(
            episodeRepository,
            resolver,
            new EpisodeChangeApplier(NullLogger<EpisodeChangeApplier>.Instance),
            new EpisodeSearchIndexCleanup(
                CreateUninitializedSearchClient(),
                NullLogger<EpisodeSearchIndexCleanup>.Instance),
            Mock.Of<IHomepagePublisher>(),
            Mock.Of<IPostManager>(),
            tweetManager ?? Mock.Of<ITweetManager>(),
            blueskyPostManager ?? Mock.Of<IBlueskyPostManager>(),
            Mock.Of<IShortnerService>(),
            Mock.Of<RedditPodcastPoster.PodcastServices.Updaters.IImageUpdater>(),
            indexer ?? Mock.Of<IEpisodeSearchIndexerService>(),
            NullLogger<EpisodeUpdateService>.Instance);
    }

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
