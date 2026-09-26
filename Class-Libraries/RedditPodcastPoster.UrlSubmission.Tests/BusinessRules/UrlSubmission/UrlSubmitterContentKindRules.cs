using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlSubmitterContentKindRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "When the submit content-type flag is on, a BBC /news/ URL returns an unpersisted NewsReport " +
        "before categorise, and nothing is saved.")]
    public async Task bbc_news_with_flag_on_returns_unpersisted_news_report()
    {
        // Arrange
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        var url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var result = await sut.Submit(
            url,
            new IndexingContext(),
            new SubmitOptions(null, MatchOtherServices: false, PersistToDatabase: true));

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.Rejected.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
        result.PlayableId.Should().BeNull();
        result.Episode.Should().BeNull();
        result.Podcast.Should().BeNull();
        _mocker.GetMock<IUrlCategoriser>().Verify(
            c => c.Categorise(
                It.IsAny<Podcast?>(),
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<NonPodcastServiceItemMetaData?>(),
                It.IsAny<bool>()),
            Times.Never);
        _mocker.GetMock<ICategorisedItemProcessor>().Verify(
            p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When a TV submit has no series name, ingest leaves that failure as an error " +
        "instead of an empty success that wrote nothing.")]
    public async Task missing_series_name_is_not_an_empty_success()
    {
        // Arrange
        var url = new Uri($"https://example.com/{_fixture.Create<string>()}");
        _mocker.GetMock<IUrlCategoriser>()
            .Setup(c => c.Categorise(
                It.IsAny<Podcast?>(),
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<NonPodcastServiceItemMetaData?>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new CategorisedItem(null, null, null, null, null, null, null, Service.Other));
        _mocker.GetMock<ICategorisedItemProcessor>()
            .Setup(p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new InvalidOperationException(
                "TV submit needs a series name before it can be stored."));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var act = () => sut.Submit(
            url,
            new IndexingContext(),
            new SubmitOptions(null, MatchOtherServices: false, PersistToDatabase: true, CreatePodcast: true));

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("TV submit needs a series name before it can be stored.");
    }
}
