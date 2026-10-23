using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class FilmRepositoryUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static CosmosException CosmosStatus(HttpStatusCode status) => new("synthetic", status, 0, string.Empty, 0);

    [Fact(DisplayName =
        "Updating a film retries when the first replace loses to a trailer attach, and the saved film keeps that trailer and the social-post flag, because a 412 is re-read instead of overwriting the attach.")]
    public async Task update_retries_precondition_failed_and_keeps_the_trailer()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var title = _fixture.CreateTitle();
        var without = new Film(title) { Id = filmId };
        var trailer = Trailer();
        var withTrailer = new Film(title) { Id = filmId, Trailers = [trailer] };
        var saved = ArrangeConflict(without, withTrailer);

        // Act
        var result = await _mocker.CreateInstance<FilmRepository>().Update(filmId, film =>
        {
            film.Tweeted = true;
            return true;
        });

        // Assert
        result.Should().BeSameAs(withTrailer);
        saved.Should().ContainSingle().Which.Should().BeSameAs(withTrailer);
        withTrailer.Tweeted.Should().BeTrue();
        withTrailer.Trailers.Should().ContainSingle().Which.Id.Should().Be(trailer.Id);
    }

    [Fact(DisplayName =
        "Renaming a film through the ETag update retries when the first replace loses to a trailer attach, and the saved film has the new name and that trailer.")]
    public async Task update_retries_precondition_failed_and_keeps_the_trailer_on_rename()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var title = _fixture.CreateTitle();
        var renamed = _fixture.CreateTitle();
        var without = new Film(title) { Id = filmId };
        var trailer = Trailer();
        var withTrailer = new Film(title) { Id = filmId, Trailers = [trailer] };
        var saved = ArrangeConflict(without, withTrailer);

        // Act
        var result = await _mocker.CreateInstance<FilmRepository>().Update(filmId, film =>
        {
            film.Name = renamed;
            return true;
        });

        // Assert
        result.Should().BeSameAs(withTrailer);
        saved.Should().ContainSingle().Which.Name.Should().Be(renamed);
        withTrailer.Trailers.Should().ContainSingle().Which.Id.Should().Be(trailer.Id);
    }

    private List<Film> ArrangeConflict(Film without, Film withTrailer)
    {
        var current = without;
        var etag = "v1";
        var saved = new List<Film>();
        var response = _mocker.GetMock<ItemResponse<Film>>();
        response.SetupGet(r => r.Resource).Returns(() => current);
        response.SetupGet(r => r.ETag).Returns(() => etag);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<Film>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => response.Object);
        var replaces = 0;
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<Film>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((Film film, string _, PartitionKey _, ItemRequestOptions _, CancellationToken _) =>
            {
                replaces++;
                if (replaces == 1)
                {
                    current = withTrailer;
                    etag = "v2";
                    throw CosmosStatus(HttpStatusCode.PreconditionFailed);
                }

                saved.Add(film);
                return Task.FromResult(response.Object);
            });
        return saved;
    }

    private FilmTrailer Trailer() => new()
    {
        Id = _fixture.CreateGuid(),
        Title = _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = _fixture.CreateDuration()
    };
}
