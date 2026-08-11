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
        "INTEGRITY: Podcast language API fil→es moves episodes still on fil to es, leaves English (null) overrides alone, " +
        "and updates denormalised podcastLanguage — null must not be treated as unset.")]
    public async Task update_moves_previous_default_followers_not_english_overrides()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Language = "fil");
        var onDefault = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = "fil";
                e.PodcastLanguage = "fil";
            })
            .Create();
        var englishOverride = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PodcastLanguage = "fil";
            })
            .Create();
        var otherOverride = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = "pt";
                e.PodcastLanguage = "fil";
            })
            .Create();

        var podcastRepo = new Mock<IPodcastRepository>();
        podcastRepo.Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(podcast);
        podcastRepo.Setup(r => r.Save(It.IsAny<Podcast>())).Returns(Task.CompletedTask);

        var episodeRepo = new Mock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.GetByPodcastId(podcast.Id))
            .Returns(ToAsyncEnumerable(onDefault, englishOverride, otherOverride));
        episodeRepo.Setup(r => r.Save(It.IsAny<Episode>())).Returns(Task.CompletedTask);

        var indexer = new Mock<IEpisodeSearchIndexerService>();
        indexer.Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

        var service = CreateService(podcastRepo.Object, episodeRepo.Object, indexer.Object);

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(podcast.Id, new PodcastChangeRequest { Language = "es" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        onDefault.Language.Should().Be("es");
        onDefault.PodcastLanguage.Should().Be("es");
        englishOverride.Language.Should().BeNull();
        englishOverride.PodcastLanguage.Should().Be("es");
        otherOverride.Language.Should().Be("pt");
        otherOverride.PodcastLanguage.Should().Be("es");
        episodeRepo.Verify(r => r.Save(onDefault), Times.Once);
        episodeRepo.Verify(r => r.Save(englishOverride), Times.Once);
        episodeRepo.Verify(r => r.Save(otherOverride), Times.Once);
    }

    [Fact(DisplayName =
        "INTEGRITY: Podcast language API null→fil moves English-default (null) episodes to fil, because they followed the previous English show default.")]
    public async Task update_from_english_default_moves_null_episodes_onto_new_default()
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
    }

    [Fact(DisplayName =
        "INTEGRITY: Podcast language API clear to English moves previous-default followers to null and leaves English overrides null.")]
    public async Task update_clearing_podcast_language_nulls_previous_default_followers()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Language = "fil");
        var onDefault = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Language = "fil";
                e.PodcastLanguage = "fil";
            })
            .Create();
        var englishOverride = _fixture.BuildEpisode()
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
            .Returns(ToAsyncEnumerable(onDefault, englishOverride));
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
        onDefault.Language.Should().BeNull();
        onDefault.PodcastLanguage.Should().BeNull();
        englishOverride.Language.Should().BeNull();
        englishOverride.PodcastLanguage.Should().BeNull();
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
