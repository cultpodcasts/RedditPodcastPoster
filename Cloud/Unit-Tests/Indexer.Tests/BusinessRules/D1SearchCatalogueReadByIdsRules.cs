using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// #1479: an incremental reconcile reads eligible ids missing from D1 outside the <c>_ts</c> window by id, with the
/// same eligibility and document rules as a full read.
/// </summary>
public class D1SearchCatalogueReadByIdsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<NewsReport> _newsReports = [];
    private readonly List<Film> _films = [];

    public D1SearchCatalogueReadByIdsRules()
    {
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<ILogger<D1SearchCatalogue>>(NullLogger<D1SearchCatalogue>.Instance);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Expression<Func<TvShowEpisode, bool>> p) => _tvEpisodes.Where(p.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Expression<Func<NewsReport, bool>> p) => _newsReports.Where(p.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> p) => _films.Where(p.Compile()).ToAsyncEnumerable());
    }

    [Fact(DisplayName =
        "Reading by id returns a document for an eligible episode whatever its _ts, no document for a removed one, " +
        "and nothing for an id no container holds, so the reconcile can tell missing from no-longer-eligible (#1479).")]
    public async Task read_by_ids_applies_the_full_rules()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var live = Episode(podcast, removed: false);
        var removed = Episode(podcast, removed: true);
        var unrelated = Episode(podcast, removed: false);
        _episodes.Seed(live, removed, unrelated);
        var gone = _fixture.CreateGuid().ToString();
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var candidates = await sut
            .ReadByIds([live.Id.ToString(), removed.Id.ToString(), gone], CancellationToken.None)
            .ToListAsync();

        // Assert
        candidates.Select(c => c.Id).Should().BeEquivalentTo([live.Id.ToString(), removed.Id.ToString()]);
        candidates.Single(c => c.Id == live.Id.ToString()).Document.Should().NotBeNull();
        candidates.Single(c => c.Id == removed.Id.ToString()).Excluded.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Reading more ids than one IN query holds returns every id from every chunk exactly once, so a gap larger " +
        "than ReadByIdsChunkSize is read in full (#1479).")]
    public async Task read_by_ids_reads_every_chunk_once()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var episodes = Enumerable.Range(0, D1SearchCatalogue.ReadByIdsChunkSize + 50)
            .Select(_ => Episode(podcast, removed: false))
            .ToArray();
        _episodes.Seed(episodes);
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var candidates = await sut
            .ReadByIds(episodes.Select(e => e.Id.ToString()).ToArray(), CancellationToken.None)
            .ToListAsync();

        // Assert
        candidates.Select(c => c.Id).Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(episodes.Select(e => e.Id.ToString()));
        candidates.Should().OnlyContain(c => c.Document != null);
    }

    [Fact(DisplayName =
        "Reading by id returns a document for an eligible TV episode, news report and film and no document for a " +
        "removed one of each, with the same rules as a full read (#1479).")]
    public async Task read_by_ids_applies_the_full_rules_to_tv_news_and_films()
    {
        // Arrange
        var tv = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), TvShowName = _fixture.CreateTitle() };
        var removedTv = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = true };
        var news = new NewsReport(_fixture.CreateTitle())
            { Id = _fixture.CreateGuid(), NewsOrganisationName = _fixture.CreateTitle() };
        var removedNews = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = true };
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        var removedFilm = new Film(_fixture.CreateTitle())
            { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration(), Removed = true };
        _tvEpisodes.AddRange([tv, removedTv]);
        _newsReports.AddRange([news, removedNews]);
        _films.AddRange([film, removedFilm]);
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var candidates = await sut
            .ReadByIds(
                new[] { tv.Id, removedTv.Id, news.Id, removedNews.Id, film.Id, removedFilm.Id }
                    .Select(id => id.ToString()).ToArray(),
                CancellationToken.None)
            .ToDictionaryAsync(c => c.Id);

        // Assert
        candidates.Should().HaveCount(6);
        candidates[tv.Id.ToString()].Document.Should().NotBeNull();
        candidates[news.Id.ToString()].Document.Should().NotBeNull();
        candidates[film.Id.ToString()].Document.Should().NotBeNull();
        candidates[removedTv.Id.ToString()].Excluded.Should().BeTrue();
        candidates[removedNews.Id.ToString()].Excluded.Should().BeTrue();
        candidates[removedFilm.Id.ToString()].Excluded.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Reading by id asks the TV, news and film containers only for ids the episode container did not return, and " +
        "skips them when every id was an episode, so a gap of episodes costs one query per chunk (#1479 review).")]
    public async Task read_by_ids_queries_later_containers_only_for_ids_not_found()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var episode = Episode(podcast, removed: false);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var candidates = await sut.ReadByIds([episode.Id.ToString()], CancellationToken.None).ToListAsync();

        // Assert
        candidates.Select(c => c.Id).Should().Equal(episode.Id.ToString());
        _mocker.GetMock<ITvShowEpisodeRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()), Times.Never);
        _mocker.GetMock<INewsReportRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()), Times.Never);
        _mocker.GetMock<IFilmRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()), Times.Never);
    }

    [Fact(DisplayName =
        "Reading by id returns no document for an episode held for approval, awaiting or rejected, as the full read " +
        "and the eligible-id projection exclude it (#1484, #1479).")]
    public async Task read_by_ids_excludes_an_episode_held_for_approval()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var awaiting = Episode(podcast, removed: false);
        awaiting.ApprovalStatus = ApprovalStatus.AwaitingApproval;
        var rejected = Episode(podcast, removed: false);
        rejected.ApprovalStatus = ApprovalStatus.Rejected;
        _episodes.Seed(awaiting, rejected);
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var candidates = await sut
            .ReadByIds([awaiting.Id.ToString(), rejected.Id.ToString()], CancellationToken.None)
            .ToListAsync();

        // Assert
        candidates.Should().HaveCount(2).And.OnlyContain(c => c.Excluded && c.Document == null);
    }

    private Episode Episode(RedditPodcastPoster.Models.Podcasts.Podcast podcast, bool removed)
    {
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        episode.Removed = removed;
        episode.ParentRemoved = null;
        return episode;
    }
}
