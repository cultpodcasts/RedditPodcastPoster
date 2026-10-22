using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace FilmSearchPusher.Tests.BusinessRules;

/// <summary>
/// Film search push rules against an in-memory films container, watermark and search index (synthetic films only).
/// </summary>
public class FilmSearchPusherRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<Film> _films = [];
    private readonly List<Film> _filmsCreatedDuringRun = [];
    private readonly List<string> _indexFilmIds = [];
    private readonly List<EpisodeSearchRecord> _pushed = [];
    private readonly List<string> _deleted = [];
    private readonly List<FilmSearchPushWatermark> _savedWatermarks = [];
    private readonly List<int> _pushBatchSizes = [];
    private readonly HashSet<string> _failingKeys = [];
    private FilmSearchPushWatermark? _watermark;
    private bool _fullReadUsed;
    private bool _incrementalReadUsed;

    public FilmSearchPusherRules()
    {
        _mocker.Use<TimeProvider>(_time);
        var films = _mocker.GetMock<IFilmRepository>();
        films.Setup(x => x.GetAll())
            .Returns(() =>
            {
                _fullReadUsed = true;
                return _films.ToAsyncEnumerable();
            });
        films.Setup(x => x.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _films.Concat(_filmsCreatedDuringRun).SingleOrDefault(f => f.Id == id));
        films.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) =>
            {
                _incrementalReadUsed = true;
                return _films.Where(selector.Compile()).ToAsyncEnumerable();
            });

        var store = _mocker.GetMock<IFilmSearchPushWatermarkStore>();
        store.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _watermark);
        store.Setup(x => x.SaveAsync(It.IsAny<FilmSearchPushWatermark>(), It.IsAny<CancellationToken>()))
            .Callback((FilmSearchPushWatermark w, CancellationToken _) => _savedWatermarks.Add(w))
            .Returns(Task.CompletedTask);

        var index = _mocker.GetMock<IFilmSearchIndexGateway>();
        index.Setup(x => x.CountFilmDocuments(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _indexFilmIds.Count);
        index.Setup(x => x.GetFilmDocumentIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _indexFilmIds.ToList());
        index.Setup(x => x.MergeOrUpload(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<EpisodeSearchRecord> docs, CancellationToken _) =>
            {
                _pushBatchSizes.Add(docs.Count);
                _pushed.AddRange(docs);
                return docs.Select(d => d.Id).Where(_failingKeys.Contains).ToList();
            });
        index.Setup(x => x.Delete(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> ids, CancellationToken _) =>
            {
                _deleted.AddRange(ids);
                return ids.Where(_failingKeys.Contains).ToList();
            });
    }

    private Film CreateStoredFilm(long timestamp, bool removed = false)
    {
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Description = _fixture.CreateTitle(),
            Timestamp = timestamp,
            Removed = removed ? true : null
        };
        _films.Add(film);
        return film;
    }

    private void GivenWatermark(long highWaterMark) =>
        _watermark = new FilmSearchPushWatermark { HighWaterMark = highWaterMark };

    private void GivenIndexAlreadyHoldsFilms(params Film[] films) =>
        _indexFilmIds.AddRange(films.Select(f => f.Id.ToString()));

    private Task<FilmSearchPushResult> Push(bool full = false, bool dryRun = false, int batchSize = 500) =>
        _mocker.CreateInstance<RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush.FilmSearchPusher>()
            .Push(new FilmSearchPushRequest(full, dryRun, batchSize), CancellationToken.None);

    [Fact(DisplayName =
        "When a watermark is saved, then only films whose _ts is at or after it are pushed, and the watermark moves to the highest _ts read, because a daily run should only send what changed.")]
    public async Task incremental_run_pushes_only_films_changed_since_watermark()
    {
        // Arrange
        var unchanged = CreateStoredFilm(timestamp: 100);
        var boundary = CreateStoredFilm(timestamp: 200);
        var changed = CreateStoredFilm(timestamp: 300);
        GivenIndexAlreadyHoldsFilms(unchanged, boundary, changed);
        GivenWatermark(200);

        // Act
        var result = await Push();

        // Assert
        _incrementalReadUsed.Should().BeTrue();
        _fullReadUsed.Should().BeFalse();
        _pushed.Select(d => d.Id).Should().BeEquivalentTo(boundary.Id.ToString(), changed.Id.ToString());
        result.FullPush.Should().BeFalse();
        result.NewWatermark.Should().Be(300);
        _savedWatermarks.Should().ContainSingle().Which.HighWaterMark.Should().Be(300);
    }

    [Fact(DisplayName =
        "When a film is pushed, then its document is the catalogue film document (contentKind Film, title from the film name, description cut to the search cap), because the push replaces the Film pull indexer.")]
    public async Task pushed_document_is_the_catalogue_film_document()
    {
        // Arrange
        var film = CreateStoredFilm(timestamp: 500);
        film.Description = string.Join(' ', Enumerable.Range(0, 40).Select(_ => _fixture.CreateTitle()));
        GivenIndexAlreadyHoldsFilms(film);
        GivenWatermark(400);

        // Act
        await Push();

        // Assert
        var document = _pushed.Should().ContainSingle().Subject;
        document.Id.Should().Be(film.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.Film);
        document.Title.Should().Be(film.Name);
        document.Description.Should().NotBeNull();
        document.Description!.Length.Should().BeLessThanOrEqualTo(RedditPodcastPoster.Search.Formatting.Constants.DescriptionSize);
    }

    [Fact(DisplayName =
        "When a changed film is marked removed, then its search document is deleted and not pushed, because removed films must leave search.")]
    public async Task removed_film_is_deleted_not_pushed()
    {
        // Arrange
        var live = CreateStoredFilm(timestamp: 300);
        var removed = CreateStoredFilm(timestamp: 310, removed: true);
        GivenIndexAlreadyHoldsFilms(live, removed);
        GivenWatermark(250);

        // Act
        var result = await Push();

        // Assert
        _pushed.Select(d => d.Id).Should().Equal(live.Id.ToString());
        _deleted.Should().Equal(removed.Id.ToString());
        result.NewWatermark.Should().Be(310);
    }

    [Fact(DisplayName =
        "When no watermark has been saved, then every film is pushed, because the first run must load all films.")]
    public async Task first_run_without_watermark_pushes_every_film()
    {
        // Arrange
        var first = CreateStoredFilm(timestamp: 10);
        var second = CreateStoredFilm(timestamp: 20);

        // Act
        var result = await Push();

        // Assert
        _fullReadUsed.Should().BeTrue();
        result.FullPush.Should().BeTrue();
        _pushed.Select(d => d.Id).Should().BeEquivalentTo(first.Id.ToString(), second.Id.ToString());
        _savedWatermarks.Should().ContainSingle().Which.HighWaterMark.Should().Be(20);
    }

    [Fact(DisplayName =
        "When a watermark exists but the index holds no film documents, then every film is pushed, because a teardown or rebuild emptied the index and an incremental run would leave films out of search.")]
    public async Task empty_index_promotes_incremental_run_to_full()
    {
        // Arrange
        var old = CreateStoredFilm(timestamp: 10);
        var recent = CreateStoredFilm(timestamp: 900);
        GivenWatermark(800);

        // Act
        var result = await Push();

        // Assert
        result.FullPush.Should().BeTrue();
        _pushed.Select(d => d.Id).Should().BeEquivalentTo(old.Id.ToString(), recent.Id.ToString());
    }

    [Fact(DisplayName =
        "When --full is asked for, then every film is pushed and film documents whose film no longer exists in Cosmos are deleted, because a hard-deleted film leaves no _ts for an incremental run to find.")]
    public async Task full_run_pushes_all_and_deletes_orphans()
    {
        // Arrange
        var kept = CreateStoredFilm(timestamp: 10);
        var orphanId = _fixture.CreateGuid().ToString();
        GivenIndexAlreadyHoldsFilms(kept);
        _indexFilmIds.Add(orphanId);
        GivenWatermark(10_000);

        // Act
        var result = await Push(full: true);

        // Assert
        _pushed.Select(d => d.Id).Should().Equal(kept.Id.ToString());
        _deleted.Should().Equal(orphanId);
        result.NewWatermark.Should().Be(10_000, "the watermark never moves backwards");
    }

    [Fact(DisplayName =
        "When a film document is missing from the films read at the start of a full run but the film exists by the time orphans are checked, then it is not deleted, because a film created during the run (for example by Move to film) already has a valid document.")]
    public async Task full_run_keeps_document_of_film_created_during_run()
    {
        // Arrange
        CreateStoredFilm(timestamp: 10);
        var createdLater = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _filmsCreatedDuringRun.Add(createdLater);
        _indexFilmIds.Add(createdLater.Id.ToString());

        // Act
        await Push(full: true);

        // Assert
        _deleted.Should().NotContain(createdLater.Id.ToString());
    }

    [Fact(DisplayName =
        "When an incremental run is made, then film documents missing from Cosmos are not looked up or deleted, because only a full run reconciles the whole index.")]
    public async Task incremental_run_does_not_reconcile_orphans()
    {
        // Arrange
        var changed = CreateStoredFilm(timestamp: 300);
        GivenIndexAlreadyHoldsFilms(changed);
        _indexFilmIds.Add(_fixture.CreateGuid().ToString());
        GivenWatermark(200);

        // Act
        await Push();

        // Assert
        _deleted.Should().BeEmpty();
        _mocker.GetMock<IFilmSearchIndexGateway>()
            .Verify(x => x.GetFilmDocumentIds(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When any push or delete fails, then the watermark is not saved and the run reports failure, so the next run retries the same films.")]
    public async Task failed_push_keeps_watermark()
    {
        // Arrange
        var good = CreateStoredFilm(timestamp: 300);
        var bad = CreateStoredFilm(timestamp: 400);
        GivenIndexAlreadyHoldsFilms(good, bad);
        GivenWatermark(200);
        _failingKeys.Add(bad.Id.ToString());

        // Act
        var result = await Push();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failed.Should().Be(1);
        result.NewWatermark.Should().Be(200);
        _savedWatermarks.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a full run partly fails, then the watermark is cleared, because the index now holds some film documents and an incremental run from the old watermark would leave older films out of search.")]
    public async Task failed_full_run_clears_watermark()
    {
        // Arrange
        var good = CreateStoredFilm(timestamp: 100);
        var bad = CreateStoredFilm(timestamp: 200);
        GivenWatermark(5_000);
        _failingKeys.Add(bad.Id.ToString());

        // Act
        var result = await Push(full: true);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.NewWatermark.Should().BeNull();
        _savedWatermarks.Should().ContainSingle().Which.HighWaterMark.Should().BeNull();
        _pushed.Select(d => d.Id).Should().Contain(good.Id.ToString());
    }

    [Fact(DisplayName =
        "When a full run's index request throws part-way (for example a storage-quota or network error), then the watermark is cleared before the error surfaces, because earlier batches are already in the index and the next run must still be full.")]
    public async Task full_run_that_throws_part_way_clears_watermark()
    {
        // Arrange
        CreateStoredFilm(timestamp: 100);
        CreateStoredFilm(timestamp: 200);
        GivenWatermark(5_000);
        var calls = 0;
        _mocker.GetMock<IFilmSearchIndexGateway>()
            .Setup(x => x.MergeOrUpload(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<EpisodeSearchRecord> docs, CancellationToken _) =>
                ++calls == 1 ? [] : throw new InvalidOperationException("synthetic request failure"));

        // Act
        var act = () => Push(full: true, batchSize: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("synthetic request failure");
        _savedWatermarks.Should().ContainSingle().Which.HighWaterMark.Should().BeNull();
    }

    [Fact(DisplayName =
        "When an incremental run's index request throws, then the watermark is not saved, because the next run retries from the same watermark.")]
    public async Task incremental_run_that_throws_keeps_watermark()
    {
        // Arrange
        var changed = CreateStoredFilm(timestamp: 300);
        GivenIndexAlreadyHoldsFilms(changed);
        GivenWatermark(200);
        _mocker.GetMock<IFilmSearchIndexGateway>()
            .Setup(x => x.MergeOrUpload(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic request failure"));

        // Act
        var act = () => Push();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _savedWatermarks.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a dry run is made, then nothing is pushed, deleted or saved, but the plan is reported, because a dry run must not touch the index or the watermark.")]
    public async Task dry_run_writes_nothing()
    {
        // Arrange
        CreateStoredFilm(timestamp: 300);
        CreateStoredFilm(timestamp: 310, removed: true);
        _indexFilmIds.Add(_fixture.CreateGuid().ToString());
        GivenWatermark(200);

        // Act
        var result = await Push(full: true, dryRun: true);

        // Assert
        result.DryRun.Should().BeTrue();
        result.ToPush.Should().Be(1);
        result.ToDelete.Should().Be(2);
        _pushed.Should().BeEmpty();
        _deleted.Should().BeEmpty();
        _savedWatermarks.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When more films changed than the batch size, then they are pushed in batches no larger than the batch size, because the search service caps a request at 1000 documents.")]
    public async Task pushes_in_batches()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            CreateStoredFilm(timestamp: 300 + i);
        }

        GivenIndexAlreadyHoldsFilms(_films.ToArray());
        GivenWatermark(200);

        // Act
        await Push(batchSize: 2);

        // Assert
        _pushBatchSizes.Should().Equal(2, 2, 1);
    }

    [Fact(DisplayName =
        "When nothing changed since the watermark, then nothing is pushed and the watermark stays where it was, because an idle day has nothing to send.")]
    public async Task idle_run_keeps_watermark()
    {
        // Arrange
        var unchanged = CreateStoredFilm(timestamp: 100);
        GivenIndexAlreadyHoldsFilms(unchanged);
        GivenWatermark(200);

        // Act
        var result = await Push();

        // Assert
        result.Succeeded.Should().BeTrue();
        _pushed.Should().BeEmpty();
        result.NewWatermark.Should().Be(200);
    }

    [Theory(DisplayName =
        "When the batch size is outside 1-1000, then the run is refused before reading anything, because the search service rejects larger requests.")]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task invalid_batch_size_is_refused(int batchSize)
    {
        // Arrange
        CreateStoredFilm(timestamp: 300);

        // Act
        var act = () => Push(batchSize: batchSize);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _fullReadUsed.Should().BeFalse();
        _incrementalReadUsed.Should().BeFalse();
    }
}
