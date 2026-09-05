using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlSubmissionRefreshMetaWiringRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public UrlSubmissionRefreshMetaWiringRules()
    {
        _mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));
        _mocker.GetMock<ISubjectEnrichmentOptionsFactory>()
            .Setup(x => x.CreateAsync(
                It.IsAny<Podcast>(),
                It.IsAny<Episode?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectEnrichmentOptions(null, null, null, string.Empty));
        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(It.IsAny<Episode>(), It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));
        _mocker.Use(NullLogger<PodcastProcessor>.Instance);
        _mocker.Use(NullLogger<CategorisedItem>.Instance);
    }

    [Fact(DisplayName =
        "When SubmitOptions.RefreshMeta is true, PodcastProcessor routes enrichment through IRefreshMetaEpisodeEnricher, " +
        "not the fill-missing IEpisodeEnricher.")]
    public async Task refresh_meta_true_uses_refresh_meta_enricher()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var categorisedItem = CreateNonPodcastCategorisedItem(podcast, episode);

        _mocker.GetMock<IRefreshMetaEpisodeEnricher>()
            .Setup(x => x.ApplyResolvedPodcastServiceProperties(
                podcast,
                categorisedItem,
                episode))
            .Returns(new ApplyResolvePodcastServicePropertiesResponse(
                SubmitResultState.None,
                SubmitResultState.Enriched,
                new SubmitEpisodeDetails(false, false, false)));

        var processor = _mocker.CreateInstance<PodcastProcessor>();

        // Act
        await processor.AddEpisodeToExistingPodcast(categorisedItem, refreshMeta: true);

        // Assert
        _mocker.GetMock<IRefreshMetaEpisodeEnricher>().Verify(
            x => x.ApplyResolvedPodcastServiceProperties(podcast, categorisedItem, episode),
            Times.Once);
        _mocker.GetMock<IEpisodeEnricher>().Verify(
            x => x.ApplyResolvedPodcastServiceProperties(
                It.IsAny<Podcast>(),
                It.IsAny<CategorisedItem>(),
                It.IsAny<Episode?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When SubmitOptions.RefreshMeta is false, PodcastProcessor routes enrichment through IEpisodeEnricher, " +
        "not IRefreshMetaEpisodeEnricher.")]
    public async Task refresh_meta_false_uses_fill_missing_enricher()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var categorisedItem = CreateNonPodcastCategorisedItem(podcast, episode);

        _mocker.GetMock<IEpisodeEnricher>()
            .Setup(x => x.ApplyResolvedPodcastServiceProperties(
                podcast,
                categorisedItem,
                episode))
            .Returns(new ApplyResolvePodcastServicePropertiesResponse(
                SubmitResultState.None,
                SubmitResultState.EpisodeAlreadyExists,
                new SubmitEpisodeDetails(false, false, false)));

        var processor = _mocker.CreateInstance<PodcastProcessor>();

        // Act
        await processor.AddEpisodeToExistingPodcast(categorisedItem, refreshMeta: false);

        // Assert
        _mocker.GetMock<IEpisodeEnricher>().Verify(
            x => x.ApplyResolvedPodcastServiceProperties(podcast, categorisedItem, episode),
            Times.Once);
        _mocker.GetMock<IRefreshMetaEpisodeEnricher>().Verify(
            x => x.ApplyResolvedPodcastServiceProperties(
                It.IsAny<Podcast>(),
                It.IsAny<CategorisedItem>(),
                It.IsAny<Episode?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When CategorisedItemProcessor receives SubmitOptions.RefreshMeta true, it forwards refreshMeta true " +
        "to PodcastProcessor.AddEpisodeToExistingPodcast.")]
    public async Task categorised_item_processor_forwards_refresh_meta_flag()
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
            .Setup(x => x.AddEpisodeToExistingPodcast(categorisedItem, true))
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
            x => x.AddEpisodeToExistingPodcast(categorisedItem, true),
            Times.Once);
    }

    private CategorisedItem CreateNonPodcastCategorisedItem(Podcast podcast, Episode episode)
    {
        var itvxUrl = new Uri(
            $"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.Itvx,
            Url: itvxUrl,
            Title: _fixture.CreateTitle());
        return new CategorisedItem(
            podcast,
            [episode],
            episode,
            null,
            null,
            null,
            nonPodcastItem,
            Service.Other);
    }
}
