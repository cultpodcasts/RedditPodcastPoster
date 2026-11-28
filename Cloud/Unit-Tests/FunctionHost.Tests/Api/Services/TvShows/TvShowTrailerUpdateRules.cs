using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Tmdb.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

/// <summary>
/// RPP #1412: the TV show PATCH <c>trailers</c> keep-list and its <c>expectedTrailerIds</c> stale guard, the same rules
/// as a film's, and the ETag-guarded save that keeps a trailer attached while the show was open for editing.
/// </summary>
public class TvShowTrailerUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private TvShow? _saved;
    private int _replaceAttempts;
    // What GetTvShowForUpdate returns on each call; the last one repeats. Empty means "the show as read".
    private readonly Queue<TvShow> _latestOnRead = new();
    private bool _replaceSucceeds = true;
    private bool _failFirstReplace;

    public TvShowTrailerUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns(() => AsyncEnumerable.Empty<TvShowEpisode>());
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchTvShowAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TmdbShowMetadata?)null);
    }

    private TvShowTrailer CreateTrailer() => new()
    {
        Id = _fixture.CreateGuid(),
        Title = _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = _fixture.CreateDuration()
    };

    private TvShow CreateShow(params TvShowTrailer[] trailers)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        if (trailers.Length > 0)
        {
            show.Trailers = [.. trailers];
        }

        var repository = _mocker.GetMock<ITvShowRepository>();
        repository.Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        repository
            .Setup(r => r.GetTvShowForUpdate(show.Id))
            .ReturnsAsync(() => new TvShowVersion(
                _latestOnRead.Count > 1 ? _latestOnRead.Dequeue() : _latestOnRead.Count == 1 ? _latestOnRead.Peek() : show,
                "etag"));
        repository
            .Setup(r => r.TryReplace(It.IsAny<TvShow>(), It.IsAny<string>()))
            .ReturnsAsync((TvShow replaced, string _) =>
            {
                _replaceAttempts++;
                if (_failFirstReplace && _replaceAttempts == 1)
                {
                    return false;
                }

                if (!_replaceSucceeds)
                {
                    return false;
                }

                _saved = replaced;
                return true;
            });
        return show;
    }

    private Task<TvShowUpdateResult> Update(TvShow show, TvShowChangeRequest change) =>
        _mocker.CreateInstance<TvShowService>()
            .UpdateAsync(new TvShowChangeRequestWrapper(show.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH TV show trailers keeps exactly the listed trailers in the listed order, renames the ones given a title, and removes the rest, because the list is the trailers to keep.")]
    public async Task trailers_keep_list_reorders_renames_and_removes()
    {
        // Arrange
        var first = CreateTrailer();
        var second = CreateTrailer();
        var third = CreateTrailer();
        var show = CreateShow(first, second, third);
        var newTitle = _fixture.CreateTitle();

        // Act
        var result = await Update(show, new TvShowChangeRequest
        {
            Trailers = [new TvShowTrailerChange(third.Id, $"  {newTitle}  "), new TvShowTrailerChange(first.Id, null)],
            ExpectedTrailerIds = [first.Id, second.Id, third.Id]
        });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.Trailers!.Select(t => t.Id).Should().Equal(third.Id, first.Id);
        _saved.Trailers![0].Title.Should().Be(newTitle);
        _saved.Trailers[1].Title.Should().Be(first.Title);
    }

    [Fact(DisplayName =
        "PATCH TV show with an empty trailers list removes every trailer, so the show stores none.")]
    public async Task empty_trailers_list_removes_all()
    {
        // Arrange
        var only = CreateTrailer();
        var show = CreateShow(only);

        // Act
        var result = await Update(show, new TvShowChangeRequest { Trailers = [], ExpectedTrailerIds = [only.Id] });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.Trailers.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV show trailers naming an id the show does not have, or listing an id twice, is a 400 and nothing is saved.")]
    public async Task unknown_or_duplicate_trailer_ids_are_bad_requests()
    {
        // Arrange
        var held = CreateTrailer();
        var show = CreateShow(held);

        // Act
        var unknown = await Update(show, new TvShowChangeRequest
        {
            Trailers = [new TvShowTrailerChange(_fixture.CreateGuid(), null)],
            ExpectedTrailerIds = [held.Id]
        });
        var duplicate = await Update(show, new TvShowChangeRequest
        {
            Trailers = [new TvShowTrailerChange(held.Id, null), new TvShowTrailerChange(held.Id, null)],
            ExpectedTrailerIds = [held.Id]
        });

        // Assert
        unknown.Status.Should().Be(TvShowUpdateStatus.BadRequest);
        duplicate.Status.Should().Be(TvShowUpdateStatus.BadRequest);
        _replaceAttempts.Should().Be(0);
    }

    [Fact(DisplayName =
        "PATCH TV show trailers when the stored show has a trailer the client did not list as expected is a 409 naming it and nothing is saved, because a stale keep-list must not delete a trailer the client never saw.")]
    public async Task stale_keep_list_is_conflict()
    {
        // Arrange
        var seen = CreateTrailer();
        var unseen = CreateTrailer();
        var show = CreateShow(seen, unseen);

        // Act
        var result = await Update(show, new TvShowChangeRequest
        {
            Trailers = [new TvShowTrailerChange(seen.Id, null)],
            ExpectedTrailerIds = [seen.Id]
        });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Conflict);
        result.UnexpectedTrailerIds.Should().Equal(unseen.Id);
        _replaceAttempts.Should().Be(0);
    }

    [Fact(DisplayName =
        "PATCH TV show trailers without expectedTrailerIds is a 409 when the show holds any trailer, because no listed ids means the client saw none.")]
    public async Task keep_list_without_expected_ids_is_conflict_when_show_has_trailers()
    {
        // Arrange
        var held = CreateTrailer();
        var show = CreateShow(held);

        // Act
        var result = await Update(show, new TvShowChangeRequest { Trailers = [new TvShowTrailerChange(held.Id, null)] });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Conflict);
        result.UnexpectedTrailerIds.Should().Equal(held.Id);
    }

    [Fact(DisplayName =
        "PATCH TV show trailers when a trailer is attached between the read and the save is a 409 naming it and nothing is saved, because the save checks the keep-list against the show that wins the ETag race.")]
    public async Task trailer_attached_between_read_and_save_is_conflict()
    {
        // Arrange
        var held = CreateTrailer();
        var attached = CreateTrailer();
        var show = CreateShow(held);
        var latest = new TvShow(show.Name) { Id = show.Id, Trailers = [held, attached] };
        _latestOnRead.Enqueue(latest);

        // Act
        var result = await Update(show, new TvShowChangeRequest
        {
            Trailers = [new TvShowTrailerChange(held.Id, null)],
            ExpectedTrailerIds = [held.Id]
        });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Conflict);
        result.UnexpectedTrailerIds.Should().Equal(attached.Id);
        _replaceAttempts.Should().Be(0);
    }

    [Fact(DisplayName =
        "A TV show edit that does not send trailers keeps the trailers attached since the edit was loaded, because the save is ETag-guarded and takes the trailers from the show that wins the race.")]
    public async Task edit_keeps_trailer_attached_while_the_show_was_open()
    {
        // Arrange
        var show = CreateShow();
        var attached = CreateTrailer();
        _latestOnRead.Enqueue(new TvShow(show.Name) { Id = show.Id, Trailers = [attached] });
        var publisher = _fixture.CreateTitle();

        // Act
        var result = await Update(show, new TvShowChangeRequest { PublisherName = publisher });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.PublisherName.Should().Be(publisher);
        _saved.Trailers.Should().ContainSingle().Which.Id.Should().Be(attached.Id);
    }

    [Fact(DisplayName =
        "A TV show edit never lowers latestReleased, because the Indexer may have raised it since the edit was loaded.")]
    public async Task edit_keeps_the_newer_latest_released()
    {
        // Arrange
        var show = CreateShow();
        show.LatestReleased = DomainTestFixture.UtcDaysAgo(30);
        var newer = DomainTestFixture.UtcDaysAgo(2);
        _latestOnRead.Enqueue(new TvShow(show.Name) { Id = show.Id, LatestReleased = newer });

        // Act
        var result = await Update(show, new TvShowChangeRequest { PublisherName = _fixture.CreateTitle() });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.LatestReleased.Should().Be(newer);
    }

    [Fact(DisplayName =
        "When the TV show changes between the save's read and replace, the save re-reads and replaces again, so the edit lands without overwriting that write.")]
    public async Task etag_conflict_is_retried()
    {
        // Arrange
        var show = CreateShow();
        _failFirstReplace = true;

        // Act
        var result = await Update(show, new TvShowChangeRequest { PublisherName = _fixture.CreateTitle() });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _replaceAttempts.Should().Be(2);
        _saved.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the TV show keeps changing on every save attempt, the PATCH fails instead of overwriting the other writes.")]
    public async Task persistent_etag_conflict_fails()
    {
        // Arrange
        var show = CreateShow();
        _replaceSucceeds = false;

        // Act
        var result = await Update(show, new TvShowChangeRequest { PublisherName = _fixture.CreateTitle() });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Failed);
        _replaceAttempts.Should().Be(3);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET TV show lists its trailers with id, title, url, image, release and duration in display order, and omits the field when there are none.")]
    public void dto_lists_trailers_in_order()
    {
        // Arrange
        var first = CreateTrailer();
        first.Image = new Uri($"https://img.example/{_fixture.CreateGuid():N}.jpg");
        first.Release = DomainTestFixture.UtcDaysAgo(3);
        var second = CreateTrailer();
        var with = new TvShow(_fixture.CreateTitle()) { Trailers = [first, second] };
        var without = new TvShow(_fixture.CreateTitle());

        // Act
        var dto = with.ToDto();
        var emptyDto = without.ToDto();

        // Assert
        dto.Trailers!.Select(t => t.Id).Should().Equal(first.Id, second.Id);
        dto.Trailers![0].Title.Should().Be(first.Title);
        dto.Trailers[0].Url.Should().Be(first.Url);
        dto.Trailers[0].Image.Should().Be(first.Image);
        dto.Trailers[0].Release.Should().Be(first.Release);
        dto.Trailers[0].Duration.Should().Be(CatalogueDtoMappingDuration(first));
        emptyDto.Trailers.Should().BeNull();
    }

    [Fact(DisplayName =
        "The PATCH body's trailers and expectedTrailerIds reach the model unchanged, so the keep-list and its stale guard are what the client sent.")]
    public void request_maps_trailers_and_expected_ids()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var title = _fixture.CreateTitle();
        var request = new global::Api.Dtos.TvShowChangeRequest
        {
            Trailers = [new global::Api.Dtos.TvShowTrailerChangeRequest { Id = id, Title = title }],
            ExpectedTrailerIds = [id]
        };

        // Act
        var model = request.ToModel();

        // Assert
        model.Trailers.Should().ContainSingle().Which.Should().Be(new TvShowTrailerChange(id, title));
        model.ExpectedTrailerIds.Should().Equal(id);
    }

    private static string CatalogueDtoMappingDuration(TvShowTrailer trailer) =>
        global::Api.Dtos.Extensions.CatalogueDtoMapping.FormatDuration(trailer.Length);
}
