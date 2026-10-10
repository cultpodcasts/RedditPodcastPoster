using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
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
    private readonly Podcast _secondPodcast;
    private readonly Episode _secondLive;
    private List<Podcast> _nameMatches;
    private EntitySearchIndexerResponse _indexResponse = new() { IndexerState = IndexerState.Executed };
    private WriteResult _writeResult = new(true);

    public RestorePodcastProcessorRules()
    {
        _podcast = _fixture.CreatePodcast(p => p.Removed = true);
        _live = _fixture.CreateStoredEpisode(_podcast, e => e.ParentRemoved = true);
        _previouslyRemoved = _fixture.CreateStoredEpisode(_podcast, e =>
        {
            e.ParentRemoved = true;
            e.Removed = true;
        });
        _secondPodcast = _fixture.CreatePodcast(p => p.Removed = true);
        _secondLive = _fixture.CreateStoredEpisode(_secondPodcast, e => e.ParentRemoved = true);
        _nameMatches = [_podcast];

        var podcasts = _mocker.GetMock<IPodcastRepository>();
        podcasts.Setup(x => x.GetPodcast(It.IsAny<Guid>())).ReturnsAsync((Podcast?)null);
        podcasts.Setup(x => x.GetPodcast(_podcast.Id)).ReturnsAsync(() => _podcast);
        podcasts.Setup(x => x.GetPodcast(_secondPodcast.Id)).ReturnsAsync(() => _secondPodcast);
        podcasts.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(() => _nameMatches.ToAsyncEnumerable());
        var episodes = _mocker.GetMock<IEpisodeRepository>();
        episodes.Setup(x => x.GetByPodcastId(_podcast.Id))
            .Returns(() => new[] { _live, _previouslyRemoved }.ToAsyncEnumerable());
        episodes.Setup(x => x.GetByPodcastId(_secondPodcast.Id))
            .Returns(() => new[] { _secondLive }.ToAsyncEnumerable());
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(It.IsAny<IEnumerable<PodcastEpisode>>()))
            .ReturnsAsync(() => _writeResult);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(x => x.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _indexResponse);
        _mocker.Use(_mocker.CreateInstance<PodcastTargetResolver>());
    }

    private RestorePodcastRequest Apply(params Guid[] ids) => new() { PodcastIds = ids, IsNonDryRun = true };

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
        var exit = await sut.Process(Apply(_podcast.Id));

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
        "Restore podcast CLI: when applied, then the episode projection is re-derived from the podcast " +
        "(podcast name included), because restore uses the same domain projection as removal.")]
    public async Task apply_rederives_episode_projection_from_podcast()
    {
        // Arrange
        _live.PodcastName = _fixture.CreateTitle();
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        await sut.Process(Apply(_podcast.Id));

        // Assert
        _live.PodcastName.Should().Be(_podcast.Name.Trim());
        _live.ParentRemoved.Should().BeFalse();
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
        "Restore podcast CLI: when neither --podcast-id nor --podcast-name is given, then it exits 2 without " +
        "writing, because there is no target.")]
    public async Task no_target_refuses()
    {
        // Arrange
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(new RestorePodcastRequest { IsNonDryRun = true });

        // Assert
        exit.Should().Be(2);
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.IsAny<Podcast>()), Times.Never);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when any --podcast-id is unknown, then it exits 2 and restores none of the targets, " +
        "because a partial target list is likely a typo.")]
    public async Task unknown_id_refuses_all()
    {
        // Arrange
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(Apply(_podcast.Id, _fixture.CreateGuid()));

        // Assert
        exit.Should().Be(2);
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.IsAny<Podcast>()), Times.Never);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when the search indexer throws, then Cosmos is still restored and it exits 1, " +
        "because the operator must re-run to finish re-indexing.")]
    public async Task indexer_throw_exits_1_after_cosmos_restore()
    {
        // Arrange
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(x => x.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(Apply(_podcast.Id));

        // Assert
        exit.Should().Be(1);
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.Is<Podcast>(p => p.Removed == false)), Times.Once);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when the indexer reports a non-executed state, then it exits 1, " +
        "because a failed re-index leaves episodes missing from search.")]
    public async Task indexer_failure_state_exits_1()
    {
        // Arrange
        _indexResponse = new EntitySearchIndexerResponse { IndexerState = IndexerState.Failure };
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(Apply(_podcast.Id));

        // Assert
        exit.Should().Be(1);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when the shortener reports failure, then it exits 1, " +
        "because short URLs remain missing until a re-run.")]
    public async Task shortner_failure_exits_1()
    {
        // Arrange
        _writeResult = new WriteResult(false);
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(Apply(_podcast.Id));

        // Assert
        exit.Should().Be(1);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when the first podcast's shortener write throws, then the second podcast is still " +
        "restored and it exits 1, because one side-effect failure must not abort the remaining targets.")]
    public async Task first_podcast_failure_still_processes_second()
    {
        // Arrange
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(It.Is<IEnumerable<PodcastEpisode>>(pe => pe.Any(p => p.Podcast.Id == _podcast.Id))))
            .ThrowsAsync(new HttpRequestException());
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();

        // Act
        var exit = await sut.Process(Apply(_podcast.Id, _secondPodcast.Id));

        // Assert
        exit.Should().Be(1);
        _secondPodcast.Removed.Should().BeFalse();
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(_secondLive), Times.Once);
        _mocker.GetMock<IShortnerService>().Verify(x => x.Write(
            It.Is<IEnumerable<PodcastEpisode>>(pe => pe.Single().Episode.Id == _secondLive.Id)), Times.Once);
    }

    [Fact(DisplayName =
        "Restore podcast CLI: when re-run after a full restore, then no podcast or episode is saved, " +
        "because restore is idempotent.")]
    public async Task rerun_after_restore_saves_nothing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<RestorePodcastProcessor>();
        await sut.Process(Apply(_podcast.Id));
        _mocker.GetMock<IPodcastRepository>().Invocations.Clear();
        _mocker.GetMock<IEpisodeRepository>().Invocations.Clear();

        // Act
        var exit = await sut.Process(Apply(_podcast.Id));

        // Assert
        exit.Should().Be(0);
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
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
