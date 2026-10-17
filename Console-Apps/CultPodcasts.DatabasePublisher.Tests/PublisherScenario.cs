using System.Linq.Expressions;
using CultPodcasts.DatabasePublisher.Tests.Fakes;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Writers;

namespace CultPodcasts.DatabasePublisher.Tests;

/// <summary>
/// In-memory Cosmos for the publisher: every repository answers from the lists below (honouring the
/// selector the publisher passes), and files land in <see cref="Writer"/>. Entities are built through the
/// same mapper the podcast-to-TV/news/film transfer uses, so a migrated item shares its id with the podcast it came from.
/// </summary>
internal sealed class PublisherScenario
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PublisherScenario()
    {
        Writer = new InMemorySafeFileEntityWriter();
        _mocker.Use<ISafeFileEntityWriter>(Writer);

        _mocker.Setup<IPodcastRepository, IAsyncEnumerable<Podcast>>(r => r.GetAll())
            .Returns(() => Podcasts.ToAsyncEnumerable());
        _mocker.Setup<IPodcastRepository, IAsyncEnumerable<Podcast>>(
                r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> selector) =>
                Podcasts.Where(selector.Compile()).ToAsyncEnumerable());
        _mocker.Setup<IPodcastRepository, Task<int>>(r => r.Count()).ReturnsAsync(() => Podcasts.Count);
        _mocker.Setup<IEpisodeRepository, Task<int>>(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid podcastId) => Episodes.Count(e => e.PodcastId == podcastId));
        _mocker.Setup<IEpisodeRepository, IAsyncEnumerable<Episode>>(
                r => r.GetByPodcastId(It.IsAny<Guid>(), It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns((Guid podcastId, Expression<Func<Episode, bool>> selector) =>
                Episodes.Where(e => e.PodcastId == podcastId).Where(selector.Compile()).ToAsyncEnumerable());

        _mocker.Setup<ITvShowRepository, IAsyncEnumerable<TvShow>>(r => r.GetAll())
            .Returns(() => TvShows.ToAsyncEnumerable());
        _mocker.Setup<ITvShowRepository, IAsyncEnumerable<TvShow>>(
                r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector) =>
                TvShows.Where(selector.Compile()).ToAsyncEnumerable());
        _mocker.Setup<ITvShowRepository, Task<int>>(r => r.Count()).ReturnsAsync(() => TvShows.Count);
        _mocker.Setup<ITvShowEpisodeRepository, Task<int>>(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid tvShowId) => TvShowEpisodes.Count(e => e.TvShowId == tvShowId));
        _mocker.Setup<ITvShowEpisodeRepository, IAsyncEnumerable<TvShowEpisode>>(
                r => r.GetByTvShowId(It.IsAny<Guid>(), It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Guid tvShowId, Expression<Func<TvShowEpisode, bool>> selector) =>
                TvShowEpisodes.Where(e => e.TvShowId == tvShowId).Where(selector.Compile()).ToAsyncEnumerable());

        _mocker.Setup<INewsOrganisationRepository, IAsyncEnumerable<NewsOrganisation>>(r => r.GetAll())
            .Returns(() => NewsOrganisations.ToAsyncEnumerable());
        _mocker.Setup<INewsOrganisationRepository, IAsyncEnumerable<NewsOrganisation>>(
                r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> selector) =>
                NewsOrganisations.Where(selector.Compile()).ToAsyncEnumerable());
        _mocker.Setup<INewsOrganisationRepository, Task<int>>(r => r.Count())
            .ReturnsAsync(() => NewsOrganisations.Count);
        _mocker.Setup<INewsReportRepository, Task<int>>(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid organisationId) => NewsReports.Count(r => r.NewsOrganisationId == organisationId));
        _mocker.Setup<INewsReportRepository, IAsyncEnumerable<NewsReport>>(
                r => r.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Guid organisationId, Expression<Func<NewsReport, bool>> selector) =>
                NewsReports.Where(r => r.NewsOrganisationId == organisationId).Where(selector.Compile())
                    .ToAsyncEnumerable());

        _mocker.Setup<IFilmRepository, IAsyncEnumerable<Film>>(r => r.GetAll())
            .Returns(() => Films.ToAsyncEnumerable());
        _mocker.Setup<IFilmRepository, IAsyncEnumerable<Film>>(
                r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => Films.Where(selector.Compile()).ToAsyncEnumerable());
        _mocker.Setup<IFilmRepository, Task<int>>(r => r.Count()).ReturnsAsync(() => Films.Count);
    }

    public InMemorySafeFileEntityWriter Writer { get; }

    public List<Podcast> Podcasts { get; } = [];
    public List<Episode> Episodes { get; } = [];
    public List<TvShow> TvShows { get; } = [];
    public List<TvShowEpisode> TvShowEpisodes { get; } = [];
    public List<NewsOrganisation> NewsOrganisations { get; } = [];
    public List<NewsReport> NewsReports { get; } = [];
    public List<Film> Films { get; } = [];

    public Podcast GivenPodcast(Action<Podcast>? customize = null)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = _fixture.CreateTitle();
            p.FileKey = FileKeyFactory.GetFileKey(p.Name);
            customize?.Invoke(p);
        });
        Podcasts.Add(podcast);
        return podcast;
    }

    public Episode GivenEpisode(Podcast podcast, int releasedDaysAgo, Action<Episode>? customize = null)
    {
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.ReleaseUtc = DomainTestFixture.UtcDaysAgo(releasedDaysAgo);
            customize?.Invoke(e);
        });
        Episodes.Add(episode);
        return episode;
    }

    public TvShow GivenTvShow(Action<TvShow>? customize = null)
    {
        var show = CatalogueParentKindMapper.ToTvShow(NewPodcast());
        customize?.Invoke(show);
        TvShows.Add(show);
        return show;
    }

    public TvShowEpisode GivenTvShowEpisode(
        TvShow show,
        int releasedDaysAgo,
        Action<TvShowEpisode>? customize = null)
    {
        var episode = CatalogueParentKindMapper.ToTvShowEpisode(NewEpisode(releasedDaysAgo), show);
        customize?.Invoke(episode);
        TvShowEpisodes.Add(episode);
        return episode;
    }

    public NewsOrganisation GivenNewsOrganisation(Action<NewsOrganisation>? customize = null)
    {
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(NewPodcast());
        customize?.Invoke(organisation);
        NewsOrganisations.Add(organisation);
        return organisation;
    }

    public NewsReport GivenNewsReport(
        NewsOrganisation organisation,
        int releasedDaysAgo,
        Action<NewsReport>? customize = null)
    {
        var report = CatalogueParentKindMapper.ToNewsReport(NewEpisode(releasedDaysAgo), organisation);
        customize?.Invoke(report);
        NewsReports.Add(report);
        return report;
    }

    public Film GivenFilm(int releasedDaysAgo = 1, Action<Film>? customize = null)
    {
        var film = CatalogueParentKindMapper.ToFilm(NewPodcast(), NewEpisode(releasedDaysAgo));
        customize?.Invoke(film);
        Films.Add(film);
        return film;
    }

    public Task Publish() => _mocker.CreateInstance<PublicDatabasePublisher>().Run();

    private Podcast NewPodcast() => _fixture.CreatePodcast(p => p.Name = _fixture.CreateTitle());

    private Episode NewEpisode(int releasedDaysAgo) =>
        _fixture.CreateEpisode(e => e.ReleaseUtc = DomainTestFixture.UtcDaysAgo(releasedDaysAgo));
}
