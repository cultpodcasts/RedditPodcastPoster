using Moq;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace FunctionHost.Tests.Api.Services.TvShows;

public static class TvShowRepositoryMockExtensions
{
    /// <summary>
    /// A TV show edit is saved with an ETag-guarded replace of the stored show (RPP #1412), so a trailer attached while
    /// the curator had it open is never overwritten. This stands in for that save: the stored show is whatever
    /// <c>GetTvShow</c> is set up to return, and <paramref name="onSaved"/> receives the show that was replaced.
    /// </summary>
    public static Mock<ITvShowRepository> SetupSaveViaReplace(
        this Mock<ITvShowRepository> repository,
        Action<TvShow> onSaved)
    {
        repository
            .Setup(r => r.GetTvShowForUpdate(It.IsAny<Guid>()))
            .Returns(async (Guid id) => await repository.Object.GetTvShow(id) is { } show
                ? new TvShowVersion(show, "etag")
                : null);
        repository
            .Setup(r => r.TryReplace(It.IsAny<TvShow>(), It.IsAny<string>()))
            .Callback<TvShow, string>((show, _) => onSaved(show))
            .ReturnsAsync(true);
        return repository;
    }
}

