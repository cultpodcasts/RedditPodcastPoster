using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace Indexer.Tests;

public class D1SearchChangeFeedTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<(D1SearchDocument[] Upserts, string[] Deletes)> _writes = [];
    private readonly Dictionary<Guid, Podcast> _podcasts = [];
    private List<Episode> _episodes = [];
    private List<TvShowEpisode> _tvEpisodes = [];
    private List<NewsReport> _reports = [];
    private List<Film> _films = [];
    private bool _active = true;

    public D1SearchChangeFeedTests()
    {
        _mocker.Use<IOptions<D1SearchOptions>>(Options.Create(new D1SearchOptions()));
        _mocker.Use(TimeProvider.System);
        var writer = _mocker.GetMock<ID1SearchIndexWriter>();
        writer.SetupGet(w => w.IsActive).Returns(() => _active);
        writer.Setup(w => w.Write(It.IsAny<IReadOnlyCollection<D1SearchDocument>>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<D1SearchDocument>, IReadOnlyCollection<string>, CancellationToken>(
                (u, d, _) => _writes.Add((u.ToArray(), d.ToArray())))
            .ReturnsAsync(new D1SearchWriteResult([]));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns(() => _episodes.ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _podcasts.GetValueOrDefault(id));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns(() => _tvEpisodes.ToAsyncEnumerable());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns(() => _reports.ToAsyncEnumerable());
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns(() => _films.ToAsyncEnumerable());
        _mocker.Use<ID1SearchCatalogue>(_mocker.CreateInstance<D1SearchCatalogue>());
    }

    [Fact(DisplayName =
        "The drip upserts a changed live episode and deletes a changed removed one, because D1 must drop a playable " +
        "that left the index rather than keep serving it.")]
    public async Task upserts_live_and_deletes_removed_episodes()
    {
        // Arrange
        var podcast = Podcast();
        var live = Episode(podcast, removed: false);
        var removed = Episode(podcast, removed: true);
        _episodes = [live, removed];
        var sut = _mocker.CreateInstance<D1SearchChangeFeed>();

        // Act
        var result = await sut.Drip(CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new { Ran = true, Succeeded = true, Upserts = 1, Deletes = 1 });
        _writes.Single().Upserts.Select(d => d.Id).Should().Equal(live.Id.ToString());
        _writes.Single().Deletes.Should().Equal(removed.Id.ToString());
    }

    [Fact(DisplayName =
        "An episode whose podcast no longer exists is deleted, because Azure's push path refuses to index it and the " +
        "indexes must hold the same corpus.")]
    public async Task deletes_an_episode_without_its_podcast()
    {
        // Arrange
        var orphan = _fixture.CreateEpisode(e => e.PodcastId = _fixture.CreateGuid());
        _episodes = [orphan];
        var sut = _mocker.CreateInstance<D1SearchChangeFeed>();

        // Act
        await sut.Drip(CancellationToken.None);

        // Assert
        _writes.Single().Deletes.Should().Equal(orphan.Id.ToString());
    }

    [Fact(DisplayName =
        "A playable that changed kind keeps its id, so when the old kind's removed document and the new kind's live one " +
        "are both in the window the upsert wins, whichever container is read first.")]
    public async Task kind_change_upsert_wins_over_the_old_kinds_delete()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        _reports = [new NewsReport(_fixture.CreateTitle()) { Id = id, Removed = true }];
        _films = [Film(id)];
        var sut = _mocker.CreateInstance<D1SearchChangeFeed>();

        // Act
        await sut.Drip(CancellationToken.None);

        // Assert
        var write = _writes.Single();
        write.Upserts.Select(d => d.Id).Should().Equal(id.ToString());
        write.Deletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Every playable kind in the window reaches D1 (episodes, TV episodes, news reports, films), because the Indexer " +
        "function's own episodes reach search only through this sync.")]
    public async Task mirrors_every_kind()
    {
        // Arrange
        var podcast = Podcast();
        _episodes = [Episode(podcast, removed: false)];
        _tvEpisodes = [new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), TvShowName = _fixture.CreateTitle() }];
        _reports = [new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), NewsOrganisationName = _fixture.CreateTitle() }];
        _films = [Film(_fixture.CreateGuid())];
        var sut = _mocker.CreateInstance<D1SearchChangeFeed>();

        // Act
        var result = await sut.Drip(CancellationToken.None);

        // Assert
        result.Upserts.Should().Be(4);
        _writes.SelectMany(w => w.Upserts).Select(d => d.ContentKind).Should()
            .BeEquivalentTo(["Episode", "TvShowEpisode", "NewsReport", "Film"]);
    }

    [Fact(DisplayName =
        "With no active D1 target the drip reads nothing from Cosmos, because a switched-off sync must cost nothing.")]
    public async Task inactive_sync_reads_nothing()
    {
        // Arrange
        _active = false;
        var sut = _mocker.CreateInstance<D1SearchChangeFeed>();

        // Act
        var result = await sut.Drip(CancellationToken.None);

        // Assert
        result.Ran.Should().BeFalse();
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()), Times.Never);
    }

    [Fact(DisplayName =
        "A Cosmos read failure is logged and returned as an incomplete run, never thrown, because the drip runs inside " +
        "the Publisher activity and must not fail the hour.")]
    public async Task cosmos_failure_is_returned_not_thrown()
    {
        // Arrange
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Throws(new InvalidOperationException(_fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<D1SearchChangeFeed>();

        // Act
        var result = await sut.Drip(CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new { Ran = true, Succeeded = false });
    }

    private Podcast Podcast()
    {
        var podcast = _fixture.CreatePodcast(p => p.Removed = false);
        _podcasts[podcast.Id] = podcast;
        return podcast;
    }

    private Episode Episode(Podcast podcast, bool removed) => _fixture.CreateEpisode(e =>
    {
        e.PodcastId = podcast.Id;
        e.Removed = removed;
        e.ParentRemoved = false;
    });

    private Film Film(Guid id)
    {
        var film = new Film(_fixture.CreateTitle()) { Id = id };
        film.SetRelease(RedditPodcastPoster.Models.Catalogue.CatalogueRelease.FromDateTimeUtc(DateTime.UtcNow));
        return film;
    }
}
