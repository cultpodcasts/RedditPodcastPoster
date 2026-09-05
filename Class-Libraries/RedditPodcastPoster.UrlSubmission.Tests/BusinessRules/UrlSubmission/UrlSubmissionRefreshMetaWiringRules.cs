using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Extensions;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlSubmissionRefreshMetaWiringRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public UrlSubmissionRefreshMetaWiringRules()
    {
        _mocker.Use(NullLogger<CategorisedItem>.Instance);
    }

    [Fact(DisplayName =
        "When AddUrlSubmission(useRefreshMetaEnricher: true), IEpisodeEnricher resolves to RefreshMetaEpisodeEnricher.")]
    public void add_url_submission_with_refresh_meta_registers_refresh_enricher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddUrlSubmission(useRefreshMetaEnricher: true);

        // Assert
        var enricher = services.Single(d => d.ServiceType == typeof(IEpisodeEnricher));
        enricher.ImplementationType.Should().Be(typeof(RefreshMetaEpisodeEnricher));
        services.Should().Contain(d => d.ServiceType == typeof(EpisodeEnricher));
    }

    [Fact(DisplayName =
        "When AddUrlSubmission defaults useRefreshMetaEnricher to false, IEpisodeEnricher is the fill-missing EpisodeEnricher.")]
    public void add_url_submission_default_registers_fill_missing_enricher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddUrlSubmission();

        // Assert
        var enricher = services.Single(d => d.ServiceType == typeof(IEpisodeEnricher));
        enricher.ImplementationType.Should().BeNull(
            "default registration is a factory that returns the concrete EpisodeEnricher");
        enricher.ImplementationFactory.Should().NotBeNull();
        services.Should().Contain(d => d.ServiceType == typeof(EpisodeEnricher));
    }

    [Fact(DisplayName =
        "When CategorisedItemProcessor receives SubmitOptions.RefreshMeta true, it still calls the single " +
        "AddEpisodeToExistingPodcast path — enricher choice is composition-root IoC, not this flag.")]
    public async Task categorised_item_processor_ignores_refresh_meta_for_enricher_routing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            null,
            null,
            null,
            null,
            Service.Other);

        _mocker.GetMock<IPodcastProcessor>()
            .Setup(x => x.AddEpisodeToExistingPodcast(categorisedItem))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.EpisodeAlreadyExists,
                SubmitResultState.None,
                Episode: episode,
                Podcast: podcast));

        var processor = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: false, RefreshMeta: true));

        // Assert
        _mocker.GetMock<IPodcastProcessor>().Verify(
            x => x.AddEpisodeToExistingPodcast(categorisedItem),
            Times.Once);
    }

    [Fact(DisplayName =
        "When UrlSubmitter.Submit receives SubmitOptions.RefreshMeta true, Categorise is called with forceMetaExtract true.")]
    public async Task url_submitter_refresh_meta_passes_force_meta_extract()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var url = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}");
        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            null,
            null,
            null,
            null,
            Service.Other);

        _mocker.GetMock<IPodcastService>()
            .Setup(s => s.GetPodcastFromEpisodeUrl(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(podcast);

        _mocker.GetMock<IUrlCategoriser>()
            .Setup(c => c.Categorise(
                It.IsAny<Podcast?>(),
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<NonPodcastServiceItemMetaData?>(),
                It.IsAny<bool>()))
            .ReturnsAsync(categorisedItem);

        _mocker.GetMock<ICategorisedItemProcessor>()
            .Setup(p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.EpisodeAlreadyExists,
                SubmitResultState.None,
                Episode: episode,
                Podcast: podcast));

        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(
            url,
            new IndexingContext(),
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: false, RefreshMeta: true));

        // Assert
        _mocker.GetMock<IUrlCategoriser>().Verify(
            c => c.Categorise(
                It.IsAny<Podcast?>(),
                url,
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<NonPodcastServiceItemMetaData?>(),
                true),
            Times.Once);
    }
}
