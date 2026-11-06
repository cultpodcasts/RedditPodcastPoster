using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.FileKeys;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class FilmRepairedFileKeyClaimRules
{
    // The repaired key is asserted as this name's slug plus _2, so the name is fixed.
    private const string FilmName = "Synthetic Feature";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryFileKeyClaimStore _claims = new();
    private readonly List<Podcast> _podcasts = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<Film> _films = [];

    public FilmRepairedFileKeyClaimRules()
    {
        _mocker.Use<IFileKeyClaimStore>(_claims);
        Filterable<IPodcastRepository, Podcast>(_podcasts);
        Filterable<ITvShowRepository, TvShow>(_shows);
        Filterable<INewsOrganisationRepository, NewsOrganisation>(_organisations);
        Filterable<IFilmRepository, Film>(_films);
    }

    [Fact(DisplayName =
        "After an unusable film key is repaired, saved with Update, and the allocator is disposed, the next film of that name still receives the _2 suffix.")]
    public async Task repaired_film_key_saved_with_update_is_kept_when_the_allocator_is_disposed()
    {
        // Arrange
        var film = new Film(FilmName) { FileKey = string.Empty };
        var response = _mocker.GetMock<ItemResponse<Film>>();
        response.SetupGet(r => r.Resource).Returns(film);
        response.SetupGet(r => r.ETag).Returns(_fixture.Create<string>());
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<Film>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
        _mocker.GetMock<Container>()
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<Film>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
        var allocator = _mocker.CreateInstance<FileKeyAllocator>();
        var repository = _mocker.CreateInstance<FilmRepository>();
        var repaired = await allocator.RepairIfUnusable(FileKeyKind.Film, FilmName, film.Id, film.FileKey);

        // Act
        var saved = await repository.Update(film.Id, latest =>
        {
            latest.Name = FilmName;
            latest.FileKey = repaired;
            return true;
        });
        await allocator.DisposeAsync();
        var next = await allocator.Allocate(FileKeyKind.Film, FilmName, _fixture.CreateGuid());

        // Assert
        saved.Should().NotBeNull();
        saved!.FileKey.Should().Be(repaired);
        next.Should().Be(repaired + "_2");
    }

    private void Filterable<TRepository, TEntity>(List<TEntity> store)
        where TRepository : class, IFilterableRepository<TEntity>
        where TEntity : CosmosSelector =>
        _mocker.GetMock<TRepository>()
            .Setup(r => r.GetAllBy(
                It.IsAny<Expression<Func<TEntity, bool>>>(),
                It.IsAny<Expression<Func<TEntity, string>>>()))
            .Returns((Expression<Func<TEntity, bool>> selector, Expression<Func<TEntity, string>> projection) =>
                store.Where(selector.Compile()).Select(projection.Compile()).ToAsyncEnumerable());
}
