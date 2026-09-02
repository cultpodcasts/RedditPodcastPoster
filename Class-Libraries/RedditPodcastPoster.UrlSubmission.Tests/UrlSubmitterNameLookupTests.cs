using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.UrlSubmission.Tests;

public class UrlSubmitterNameLookupTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IReadOnlyList<Podcast> _nameMatches = [];

    public UrlSubmitterNameLookupTests()
    {
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(() => ToAsyncEnumerable(_nameMatches));
    }

    [Fact(DisplayName =
        "When several podcasts share a name and submit is name-only, ingest throws instead of attaching to the first Cosmos row.")]
    public async Task name_only_ambiguous_throws_before_categorise()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var first = _fixture.CreatePodcast(p => p.Name = name);
        var second = _fixture.CreatePodcast(p => p.Name = name);
        _nameMatches = [first, second];
        var sut = _mocker.CreateInstance<UrlSubmitter>();
        var url = new Uri($"https://example.com/{_fixture.Create<string>()}");

        // Act
        var act = () => sut.Submit(
            url,
            new IndexingContext(),
            new SubmitOptions(null, true, PodcastName: name));

        // Assert
        var ex = await act.Should().ThrowAsync<AmbiguousPodcastNameException>();
        ex.Which.PodcastIds.Should().BeEquivalentTo([first.Id, second.Id]);
        _mocker.GetMock<IUrlCategoriser>().Verify(
            c => c.Categorise(
                It.IsAny<Podcast?>(),
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>()),
            Times.Never);
        _mocker.GetMock<ICategorisedItemProcessor>().Verify(
            p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When exactly one podcast has the submitted name, submit categorises against that podcast instead of URL discovery.")]
    public async Task unique_name_categorises_against_that_podcast()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast(p => p.Name = name);
        _nameMatches = [podcast];
        var sut = _mocker.CreateInstance<UrlSubmitter>();
        var url = new Uri($"https://example.com/{_fixture.Create<string>()}");

        // Act
        await sut.Submit(
            url,
            new IndexingContext(),
            new SubmitOptions(null, true, PodcastName: name));

        // Assert
        _mocker.GetMock<IUrlCategoriser>().Verify(
            c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == podcast.Id),
                url,
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When submit already includes podcastId, name lookup is skipped so a curator choice is not overwritten.")]
    public async Task podcast_id_skips_name_lookup()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var chosen = _fixture.CreatePodcast(p => p.Name = name);
        var other = _fixture.CreatePodcast(p => p.Name = name);
        _nameMatches = [other];
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(chosen.Id))
            .ReturnsAsync(chosen);
        var sut = _mocker.CreateInstance<UrlSubmitter>();
        var url = new Uri($"https://example.com/{_fixture.Create<string>()}");

        // Act
        await sut.Submit(
            url,
            new IndexingContext(),
            new SubmitOptions(chosen.Id, true, PodcastName: name));

        // Assert
        _mocker.GetMock<IPodcastRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()),
            Times.Never);
        _mocker.GetMock<IUrlCategoriser>().Verify(
            c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == chosen.Id),
                url,
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When submit references a podcast id that does not exist, ingest throws instead of creating a series.")]
    public async Task missing_podcast_id_throws_before_categorise()
    {
        // Arrange
        var missingId = _fixture.CreateGuid();
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(missingId))
            .ReturnsAsync((Podcast?)null);
        var sut = _mocker.CreateInstance<UrlSubmitter>();
        var url = new Uri($"https://example.com/{_fixture.Create<string>()}");

        // Act
        var act = () => sut.Submit(
            url,
            new IndexingContext(),
            new SubmitOptions(missingId, true));

        // Assert
        var ex = await act.Should().ThrowAsync<SubmitPodcastNotFoundException>();
        ex.Which.PodcastId.Should().Be(missingId);
        _mocker.GetMock<IUrlCategoriser>().Verify(
            c => c.Categorise(
                It.IsAny<Podcast?>(),
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    private static async IAsyncEnumerable<Podcast> ToAsyncEnumerable(IEnumerable<Podcast> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
