using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RemoveEpisodes.Tests.BusinessRules;

/// <summary>
/// The restore-episodes CLI clears <c>removed</c> on the episodes listed in a file, except one held for curator
/// approval (#1484), which only a curator's approval makes public.
/// </summary>
public class RestoreProcessorRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly string _file = Path.GetTempFileName();
    private Episode[] _matches = [];

    public RestoreProcessorRules()
    {
        _podcast = _fixture.CreatePodcast();
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns(() => _matches.ToAsyncEnumerable());
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(x => x.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse());
    }

    public void Dispose() => File.Delete(_file);

    private async Task<RestoreRequest> RequestFor(Episode episode)
    {
        await File.WriteAllLinesAsync(_file, [$"'{episode.PodcastName}' - '{episode.Title}'."]);
        return new RestoreRequest { Filename = _file };
    }

    private Episode RemovedEpisode(ApprovalStatus? status)
    {
        var episode = _fixture.CreateStoredEpisode(_podcast, e =>
        {
            e.PodcastName = "Synthetic Podcast";
            e.Title = "Synthetic Episode Title";
            e.Removed = true;
            e.ApprovalStatus = status;
        });
        _matches = [episode];
        return episode;
    }

    [Theory(DisplayName =
        "Restore episodes CLI: an episode held for approval (awaiting approval or rejected) is skipped with a warning: " +
        "it stays removed and is neither saved nor indexed, because only a curator's approval makes it public (#1484).")]
    [InlineData(ApprovalStatus.AwaitingApproval)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task held_episode_is_not_restored(ApprovalStatus status)
    {
        // Arrange
        var episode = RemovedEpisode(status);
        var request = await RequestFor(episode);
        var sut = _mocker.CreateInstance<RestoreProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        episode.Removed.Should().BeTrue();
        episode.ApprovalStatus.Should().Be(status);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(
            x => x.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Restore episodes CLI: an ordinary removed episode (no approval status) is un-removed, saved and indexed.")]
    public async Task ordinary_episode_is_restored()
    {
        // Arrange
        var episode = RemovedEpisode(null);
        var request = await RequestFor(episode);
        var sut = _mocker.CreateInstance<RestoreProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        episode.Removed.Should().BeFalse();
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(episode), Times.Once);
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(
            x => x.IndexEpisodes(It.Is<IEnumerable<Guid>>(ids => ids.Single() == episode.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
