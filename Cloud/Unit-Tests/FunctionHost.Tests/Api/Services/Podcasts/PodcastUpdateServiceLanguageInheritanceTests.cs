using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Models;
using Api.Services.Podcasts;
using Azure.Search.Documents;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastUpdateServiceLanguageInheritanceTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Podcast language update: when an episode has null Language, then propagation sets episode Language to the podcast language and saves, because unset episode langs inherit the podcast default.")]
    public async Task update_propagates_podcast_language_onto_unset_episode_language()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Language = null);
        var unsetEpisode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PodcastLanguage = null;
            })
            .Create();
        var explicitEpisode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = "es";
                e.PodcastLanguage = null;
            })
            .Create();

        var podcastRepo = new Mock<IPodcastRepository>();
        podcastRepo.Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        podcastRepo.Setup(r => r.Save(It.IsAny<Podcast>())).Returns(Task.CompletedTask);

        var episodeRepo = new Mock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.GetByPodcastId(podcast.Id))
            .Returns(ToAsyncEnumerable(unsetEpisode, explicitEpisode));
        episodeRepo.Setup(r => r.Save(It.IsAny<Episode>())).Returns(Task.CompletedTask);

        var indexer = new Mock<IEpisodeSearchIndexerService>();
        indexer.Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

        var service = CreateService(podcastRepo.Object, episodeRepo.Object, indexer.Object);

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { Language = "fil" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        unsetEpisode.Language.Should().Be("fil");
        unsetEpisode.PodcastLanguage.Should().Be("fil");
        explicitEpisode.Language.Should().Be("es");
        explicitEpisode.PodcastLanguage.Should().Be("fil");
        episodeRepo.Verify(r => r.Save(unsetEpisode), Times.Once);
        episodeRepo.Verify(r => r.Save(explicitEpisode), Times.Once);
    }

    [Fact(DisplayName =
        "Podcast language clear: when request Language is empty, then unset episode Language stays null, because clearing the podcast default must not invent an episode language.")]
    public async Task update_clearing_podcast_language_does_not_invent_episode_language()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Language = "fil");
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PodcastLanguage = "fil";
            })
            .Create();

        var podcastRepo = new Mock<IPodcastRepository>();
        podcastRepo.Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        podcastRepo.Setup(r => r.Save(It.IsAny<Podcast>())).Returns(Task.CompletedTask);

        var episodeRepo = new Mock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.GetByPodcastId(podcast.Id))
            .Returns(ToAsyncEnumerable(episode));
        episodeRepo.Setup(r => r.Save(It.IsAny<Episode>())).Returns(Task.CompletedTask);

        var indexer = new Mock<IEpisodeSearchIndexerService>();
        indexer.Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

        var service = CreateService(podcastRepo.Object, episodeRepo.Object, indexer.Object);

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { Language = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        podcast.Language.Should().BeNull();
        episode.Language.Should().BeNull();
        episode.PodcastLanguage.Should().BeNull();
    }

    private static PodcastUpdateService CreateService(
        IPodcastRepository podcastRepository,
        IEpisodeRepository episodeRepository,
        IEpisodeSearchIndexerService indexer) =>
        new(
            podcastRepository,
            episodeRepository,
            indexer,
            CreateUninitializedSearchClient(),
            Mock.Of<IShortnerService>(),
            new PodcastChangeApplier(NullLogger<PodcastChangeApplier>.Instance),
            new PodcastEpisodeProjectionHelper(episodeRepository),
            NullLogger<PodcastUpdateService>.Instance);

    private static async IAsyncEnumerable<Episode> ToAsyncEnumerable(params Episode[] episodes)
    {
        foreach (var episode in episodes)
        {
            yield return episode;
            await Task.Yield();
        }
    }

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
