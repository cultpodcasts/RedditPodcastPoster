using System.Linq.Expressions;
using Api.Models;
using Api.Services.Discovery;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryRemovedPodcastSaveRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Podcast> _podcasts = [];

    public DiscoveryRemovedPodcastSaveRules()
    {
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.TryClaimForProcessing(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.UpdateDiscoveryInfoContent())
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> predicate) => ToAsync(_podcasts.Where(predicate.Compile())));
    }

    [Fact(DisplayName =
        "Saving a discovery result whose podcast is removed does not ingest it and leaves it out of the accepted ids, because a banned channel is rejected with the rest of the queue.")]
    public async Task removed_podcast_result_is_rejected_on_save()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        _podcasts.Add(podcast);
        var result = new DiscoveryResult { Id = _fixture.CreateGuid(), MatchingPodcastIds = [podcast.Id] };
        var documentId = _fixture.CreateGuid();
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync([result]);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var outcome = await sut.SubmitAsync(
            new DiscoverySubmitRequest { DiscoveryResultsDocumentIds = [documentId], ResultIds = [result.Id] },
            CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().Verify(
            s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()),
            Times.Never);
        _mocker.GetMock<IDiscoveryResultsService>().Verify(s => s.MarkAsProcessed(
            It.Is<Guid[]>(ids => ids.SequenceEqual(new[] { documentId })),
            It.Is<Guid[]>(ids => ids.Length == 0),
            It.Is<Guid[]>(ids => ids.Length == 0)), Times.Once);
    }

    [Fact(DisplayName =
        "Saving a discovery result for a live playlist-scoped show still ingests it when the only removed podcast on that channel is the outlet, because the outlet does not own the playlist show.")]
    public async Task live_playlist_show_is_saved_when_the_outlet_is_removed()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        _podcasts.Add(_fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
            p.Removed = true;
        }));
        var show = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        _podcasts.Add(show);
        var result = new DiscoveryResult { Id = _fixture.CreateGuid(), YouTubeChannelId = channelId };
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync([result]);
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(s => s.Submit(result, It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new DiscoverySubmitResult(DiscoverySubmitResultState.NoUrls));
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var outcome = await sut.SubmitAsync(
            new DiscoverySubmitRequest
            {
                DiscoveryResultsDocumentIds = [_fixture.CreateGuid()],
                ResultIds = [result.Id]
            },
            CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().Verify(
            s => s.Submit(result, It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()), Times.Once);
    }

    [Fact(DisplayName =
        "Saving a discovery result whose only channel match is a removed outlet does not ingest it, because a banned channel with no other show is still rejected.")]
    public async Task removed_outlet_with_no_playlist_show_is_rejected_on_save()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        _podcasts.Add(_fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = string.Empty;
            p.Removed = true;
        }));
        var result = new DiscoveryResult { Id = _fixture.CreateGuid(), YouTubeChannelId = channelId };
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDiscoveryResult(It.IsAny<DiscoverySubmitRequest>()))
            .ReturnsAsync([result]);
        var sut = _mocker.CreateInstance<DiscoveryCurationSubmitService>();

        // Act
        var outcome = await sut.SubmitAsync(
            new DiscoverySubmitRequest
            {
                DiscoveryResultsDocumentIds = [_fixture.CreateGuid()],
                ResultIds = [result.Id]
            },
            CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryCurationSubmitStatus.Ok);
        _mocker.GetMock<IDiscoveryUrlSubmitter>().Verify(
            s => s.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()),
            Times.Never);
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items.ToList())
        {
            yield return item;
            await Task.Yield();
        }
    }
}
