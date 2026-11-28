using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace MergeTvShows.Tests.BusinessRules;

public class MergeTvShowsTrailerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Dictionary<Guid, TvShow> _shows = [];
    private readonly List<Guid> _deletedShowIds = [];

    public MergeTvShowsTrailerRules()
    {
        var shows = _mocker.GetMock<ITvShowRepository>();
        shows.Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.GetValueOrDefault(id));
        shows.Setup(r => r.Update(It.IsAny<Guid>(), It.IsAny<Func<TvShow, bool>>()))
            .Returns<Guid, Func<TvShow, bool>>((id, mutate) =>
            {
                if (!_shows.TryGetValue(id, out var show))
                {
                    return Task.FromResult<TvShow?>(null);
                }

                mutate(show);
                return Task.FromResult<TvShow?>(show);
            });
        shows.Setup(r => r.Delete(It.IsAny<Guid>()))
            .Callback<Guid>(id =>
            {
                _deletedShowIds.Add(id);
                _shows.Remove(id);
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns(Array.Empty<TvShowEpisode>().ToAsyncEnumerable());
        _mocker.GetMock<IFileKeyAllocator>()
            .Setup(a => a.AllocateForRename(It.IsAny<FileKeyKind>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((FileKeyKind _, string _, Guid _, string? current) => current ?? _fixture.Create<string>());
    }

    [Fact(DisplayName =
        "Applying a merge moves a source show's trailers onto the target, after the target's own, before the source is deleted, because the trailer's episode row is already gone and deleting the show would lose it (RPP #1412).")]
    public async Task apply_moves_source_trailers_onto_the_target_before_deleting_the_source()
    {
        // Arrange
        var targetTrailer = CreateTrailer();
        var sourceTrailer = CreateTrailer();
        var target = AddShow(targetTrailer);
        var source = AddShow(sourceTrailer);
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        var exitCode = await sut.Process(Request(target, source, apply: true), CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _deletedShowIds.Should().Equal(source.Id);
        target.Trailers!.Select(trailer => trailer.Id).Should().Equal(targetTrailer.Id, sourceTrailer.Id);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(It.IsAny<TvShow>()), Times.Never);
    }

    [Fact(DisplayName =
        "A trailer both the source and the target hold is kept once on the target, because a trailer id is unique on a show.")]
    public async Task apply_does_not_duplicate_a_trailer_the_target_already_holds()
    {
        // Arrange
        var shared = CreateTrailer();
        var target = AddShow(shared);
        var source = AddShow(shared);
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        var exitCode = await sut.Process(Request(target, source, apply: true), CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        target.Trailers.Should().ContainSingle().Which.Id.Should().Be(shared.Id);
    }

    [Fact(DisplayName =
        "A trailer attached to a source show after the merge plan was read is moved onto the target just before that source is deleted, so the late trailer is not lost with the show.")]
    public async Task apply_moves_a_trailer_attached_to_the_source_after_the_plan_was_read()
    {
        // Arrange
        var target = AddShow();
        var source = AddShow();
        var late = CreateTrailer();
        var reads = 0;
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(source.Id))
            .ReturnsAsync(() =>
            {
                // The plan reads the source once; the pre-delete re-read sees the trailer attached since.
                if (++reads > 1)
                {
                    source.Trailers = [late];
                }

                return source;
            });
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        var exitCode = await sut.Process(Request(target, source, apply: true), CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        target.Trailers.Should().ContainSingle().Which.Id.Should().Be(late.Id);
        _deletedShowIds.Should().Equal(source.Id);
    }

    [Fact(DisplayName =
        "A dry-run merge writes nothing: no target update, no trailer move and no source delete, because writes need --apply.")]
    public async Task dry_run_does_not_move_trailers_or_delete_sources()
    {
        // Arrange
        var target = AddShow();
        var source = AddShow(CreateTrailer());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        var exitCode = await sut.Process(Request(target, source, apply: false), CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        target.Trailers.Should().BeNull();
        _deletedShowIds.Should().BeEmpty();
        _mocker.GetMock<ITvShowRepository>()
            .Verify(r => r.Update(It.IsAny<Guid>(), It.IsAny<Func<TvShow, bool>>()), Times.Never);
    }

    private MergeTvShowsRequest Request(TvShow target, TvShow source, bool apply) => new()
    {
        TargetId = target.Id,
        TargetName = _fixture.CreateTitle(),
        SourceIds = [source.Id],
        Apply = apply
    };

    private TvShow AddShow(params TvShowTrailer[] trailers)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        if (trailers.Length > 0)
        {
            show.Trailers = [.. trailers];
        }

        _shows[show.Id] = show;
        return show;
    }

    private TvShowTrailer CreateTrailer() => new()
    {
        Id = _fixture.CreateGuid(),
        Title = _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = _fixture.CreateDuration()
    };
}
