using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services;

public class SubmitUrlServiceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IReadOnlyList<Podcast> _nameMatches = [];
    private SubmitOptions? _capturedOptions;

    public SubmitUrlServiceTests()
    {
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(() => ToAsyncEnumerable(_nameMatches));

        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync((Uri _, IndexingContext __, SubmitOptions options) =>
            {
                _capturedOptions = options;
                return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
            });

        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisode(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse());
    }

    [Fact(DisplayName =
        "When several podcasts share a name and submit is name-only, the API returns conflict instead of attaching to the first Cosmos row.")]
    public async Task name_only_ambiguous_returns_conflict_without_submit()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var first = _fixture.CreatePodcast(p => p.Name = name);
        var second = _fixture.CreatePodcast(p => p.Name = name);
        _nameMatches = [first, second];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Conflict);
        result.AmbiguousPodcasts.Should().BeEquivalentTo([first.Id, second.Id]);
        _mocker.GetMock<IUrlSubmitter>().Verify(
            s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When exactly one podcast has the submitted name, submit attaches using that podcast's id.")]
    public async Task unique_name_resolves_to_podcast_id()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast(p => p.Name = name);
        _nameMatches = [podcast];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions.Should().NotBeNull();
        _capturedOptions!.PodcastId.Should().Be(podcast.Id);
        _capturedOptions.PodcastName.Should().Be(name);
    }

    [Fact(DisplayName =
        "When submit already includes podcastId, name lookup is skipped so a curator choice is not overwritten.")]
    public async Task podcast_id_is_not_replaced_by_name_lookup()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var chosen = _fixture.CreateGuid();
        var other = _fixture.CreatePodcast(p => p.Name = name);
        _nameMatches = [other];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastId = chosen,
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions!.PodcastId.Should().Be(chosen);
        _mocker.GetMock<IPodcastRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the submitted name matches no podcast, submit continues without an id so a series can be created.")]
    public async Task missing_name_leaves_podcast_id_unset()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _nameMatches = [];
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions!.PodcastId.Should().BeNull();
        _capturedOptions.PodcastName.Should().Be(name);
    }

    [Fact(DisplayName =
        "When the submitter reports an ambiguous name, the API maps that to conflict with the podcast id list.")]
    public async Task submitter_ambiguous_name_maps_to_conflict()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var firstId = _fixture.CreateGuid();
        var secondId = _fixture.CreateGuid();
        _nameMatches = [];
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new AmbiguousPodcastNameException(name, [firstId, secondId]));
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.Create<string>()}"),
            PodcastName = name
        };
        var sut = _mocker.CreateInstance<SubmitUrlService>();

        // Act
        var result = await sut.SubmitAsync(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Conflict);
        result.AmbiguousPodcasts.Should().BeEquivalentTo([firstId, secondId]);
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
