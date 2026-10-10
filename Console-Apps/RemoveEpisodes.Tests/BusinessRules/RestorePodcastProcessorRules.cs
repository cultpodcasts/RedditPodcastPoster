using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.UrlShortening.Services;
using RemoveEpisodes.PodcastRestore;

namespace RemoveEpisodes.Tests.BusinessRules;

public class RestorePodcastProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly Episode _live;
    private readonly Episode _previouslyRemoved;
    private List<Podcast> _nameMatches;

    public RestorePodcastProcessorRules()
    {
        _podcast = _fixture.CreatePodcast(p => p.Removed = true);
        _live = _fixture.CreateStoredEpisode(_podcast, e => e.ParentRemoved = true);
        _previouslyRemoved = _fixture.CreateStoredEpisode(_podcast, e =>
        {
            e.ParentRemoved = true;
            e.Removed = true;
        });
        _nameMatches = [_podcast];

        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetPodcast(_podcast.Id)).ReturnsAsync(() => _podcast);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(() => _nameMatches.ToAsyncEnumerable());
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(x => x.GetByPodcastId(_podcast.Id))
            .Returns(() => new[] { _live, _previouslyRemoved }.ToAsyncEnumerable());
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(It.IsAny<IEnumerable<PodcastEpisode>>()))
            .ReturnsAsync(new WriteResult(true));
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(x => x.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse());
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when run without --non-dry-run, then nothing is saved, indexed or written to the " +
        "shortener, because the default is a dry run.")]
    public async Task dry_run_writes_nothing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(new RestorePodcastRequest { PodcastIds = [_podcast.Id] });

        // Assert
        exit.Should().Be(0);
        _podcast.Removed.Should().BeTrue();
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(
            x => x.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IShortnerService>().Verify(x => x.Write(It.IsAny<IEnumerable<PodcastEpisode>>()), Times.Never);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when applied, then the podcast and every episode are saved un-parent-removed and " +
        "only episodes not themselves removed are re-indexed and re-shortened, because those were the ones live " +
        "before the accidental removal.")]
    public async Task apply_saves_and_republishes_live_episodes_only()
    {
        // Arrange
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(new RestorePodcastRequest { PodcastIds = [_podcast.Id], IsNonDryRun = true });

        // Assert
        exit.Should().Be(0);
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.Is<Podcast>(p => p.Removed == false)), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.Is<Episode>(e => e.ParentRemoved == false)),
            Times.Exactly(2));
        _previouslyRemoved.Removed.Should().BeTrue();
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(x => x.IndexEpisodes(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { _live.Id })), It.IsAny<CancellationToken>()));
        _mocker.GetMock<IShortnerService>().Verify(x => x.Write(
            It.Is<IEnumerable<PodcastEpisode>>(pe => pe.Single().Episode.Id == _live.Id)));
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when a --podcast-name matches more than one podcast, then it exits 2 without " +
        "writing, because the target must be unambiguous.")]
    public async Task ambiguous_name_refuses()
    {
        // Arrange
        _nameMatches = [_podcast, _fixture.CreatePodcast()];
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(new RestorePodcastRequest
            { PodcastNames = [_podcast.Name], IsNonDryRun = true });

        // Assert
        exit.Should().Be(2);
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.IsAny<Podcast>()), Times.Never);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when --skip-shortner is set, then short URLs are not re-created, because KV writes " +
        "can be done separately with KVWriter.")]
    public async Task skip_shortner_skips_kv_writes()
    {
        // Arrange
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        await sut.Process(new RestorePodcastRequest
            { PodcastIds = [_podcast.Id], IsNonDryRun = true, SkipShortner = true });

        // Assert
        _mocker.GetMock<IShortnerService>().Verify(x => x.Write(It.IsAny<IEnumerable<PodcastEpisode>>()), Times.Never);
    }
}
