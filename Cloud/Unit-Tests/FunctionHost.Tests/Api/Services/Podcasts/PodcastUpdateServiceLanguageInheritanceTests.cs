using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
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
    private readonly AutoMocker _mocker = new();
    private Podcast _podcast = null!;
    private Episode[] _episodes = [];

    public PodcastUpdateServiceLanguageInheritanceTests()
    {
        _mocker.Use(CreateUninitializedSearchClient());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(() => _podcast);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.Save(It.IsAny<Podcast>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetByPodcastId(It.IsAny<Guid>()))
            .Returns(() => ToAsyncEnumerable(_episodes));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });
        _mocker.GetMock<IShortnerService>();
        _mocker.GetMock<ILogger<PodcastChangeApplier>>();
        _mocker.GetMock<ILogger<PodcastUpdateService>>();
    }

    [Fact(DisplayName =
        "INTEGRITY: Podcast language API fil→es moves episodes still on fil to es, leaves English (null) overrides alone, " +
        "and updates denormalised PublisherLanguage — null must not be treated as unset.")]
    public async Task update_moves_previous_default_followers_not_english_overrides()
    {
        // Arrange
        _podcast = _fixture.CreatePodcast(p => p.Language = "fil");
        var onDefault = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .Customize(e =>
            {
                e.Language = "fil";
                e.PublisherLanguage = "fil";
            })
            .Create();
        var englishOverride = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PublisherLanguage = "fil";
            })
            .Create();
        var otherOverride = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .Customize(e =>
            {
                e.Language = "pt";
                e.PublisherLanguage = "fil";
            })
            .Create();
        _episodes = [onDefault, englishOverride, otherOverride];
        var service = _mocker.CreateInstance<PodcastUpdateService>();

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(_podcast.Id, new PodcastChangeRequest { Language = "es" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        onDefault.Language.Should().Be("es");
        onDefault.PublisherLanguage.Should().Be("es");
        englishOverride.Language.Should().BeNull();
        englishOverride.PublisherLanguage.Should().Be("es");
        otherOverride.Language.Should().Be("pt");
        otherOverride.PublisherLanguage.Should().Be("es");
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.Save(onDefault), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.Save(englishOverride), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.Save(otherOverride), Times.Once);
    }

    [Fact(DisplayName =
        "INTEGRITY: Podcast language API null→fil moves English-default (null) episodes to fil, because they followed the previous English show default.")]
    public async Task update_from_english_default_moves_null_episodes_onto_new_default()
    {
        // Arrange
        _podcast = _fixture.CreatePodcast(p => p.Language = null);
        var unsetEpisode = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PublisherLanguage = null;
            })
            .Create();
        var explicitEpisode = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .Customize(e =>
            {
                e.Language = "es";
                e.PublisherLanguage = null;
            })
            .Create();
        _episodes = [unsetEpisode, explicitEpisode];
        var service = _mocker.CreateInstance<PodcastUpdateService>();

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(_podcast.Id, new PodcastChangeRequest { Language = "fil" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        unsetEpisode.Language.Should().Be("fil");
        unsetEpisode.PublisherLanguage.Should().Be("fil");
        explicitEpisode.Language.Should().Be("es");
        explicitEpisode.PublisherLanguage.Should().Be("fil");
    }

    [Fact(DisplayName =
        "INTEGRITY: Podcast language API clear to English moves previous-default followers to null and leaves English overrides null.")]
    public async Task update_clearing_podcast_language_nulls_previous_default_followers()
    {
        // Arrange
        _podcast = _fixture.CreatePodcast(p => p.Language = "fil");
        var onDefault = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .Customize(e =>
            {
                e.Language = "fil";
                e.PublisherLanguage = "fil";
            })
            .Create();
        var englishOverride = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .Customize(e =>
            {
                e.Language = null;
                e.PublisherLanguage = "fil";
            })
            .Create();
        _episodes = [onDefault, englishOverride];
        var service = _mocker.CreateInstance<PodcastUpdateService>();

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(_podcast.Id, new PodcastChangeRequest { Language = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _podcast.Language.Should().BeNull();
        onDefault.Language.Should().BeNull();
        onDefault.PublisherLanguage.Should().BeNull();
        englishOverride.Language.Should().BeNull();
        englishOverride.PublisherLanguage.Should().BeNull();
    }

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
