using System.Linq.Expressions;
using CultPodcasts.DatabasePublisher.Consistency;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
using RedditPodcastPoster.Persistence.Providers;

namespace CultPodcasts.DatabasePublisher.Tests;

/// <summary>
/// In-memory Cosmos for the publisher: every repository answers from the lists below (honouring the
/// selector the publisher passes), and the publisher works in a real temporary output folder
/// (<see cref="OutputDirectory"/>) that tests can pre-populate with the files of a previous publish. Entities
/// are built through the same mapper the podcast-to-TV/news/film transfer uses, so a migrated item shares its id
/// with the podcast it came from and a film takes the id of its episode. One publisher instance serves every
/// <see cref="Publish"/> call of a scenario, as the singleton does in the real host.
/// </summary>
internal sealed class PublisherScenario : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PublicDatabasePublisher? _publisher;

    public PublisherScenario()
    {
        Directory.CreateDirectory(OutputDirectory);
        _mocker.Use(new PublicationFolder(OutputDirectory, new JsonSerializerOptionsProvider()));

        _mocker.Setup<IPodcastRepository, IAsyncEnumerable<Podcast>>(r => r.GetAll())
            .Returns(() => Podcasts.ToAsyncEnumerable());
        _mocker.Setup<IEpisodeRepository, Task<int>>(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid podcastId) => Episodes.Count(e => e.PodcastId == podcastId));
        _mocker.Setup<IEpisodeRepository, IAsyncEnumerable<Episode>>(
                r => r.GetByPodcastId(It.IsAny<Guid>(), It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns((Guid podcastId, Expression<Func<Episode, bool>> selector) =>
                Episodes.Where(e => e.PodcastId == podcastId).Where(selector.Compile()).ToAsyncEnumerable());

        _mocker.Setup<ITvShowRepository, IAsyncEnumerable<TvShow>>(r => r.GetAll())
            .Returns(() => TvShows.ToAsyncEnumerable());
        _mocker.Setup<ITvShowEpisodeRepository, Task<int>>(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid tvShowId) => TvShowEpisodes.Count(e => e.TvShowId == tvShowId));
        _mocker.Setup<ITvShowEpisodeRepository, IAsyncEnumerable<TvShowEpisode>>(
                r => r.GetByTvShowId(It.IsAny<Guid>(), It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Guid tvShowId, Expression<Func<TvShowEpisode, bool>> selector) =>
                TvShowEpisodes.Where(e => e.TvShowId == tvShowId).Where(selector.Compile()).ToAsyncEnumerable());

        _mocker.Setup<INewsOrganisationRepository, IAsyncEnumerable<NewsOrganisation>>(r => r.GetAll())
            .Returns(() => NewsOrganisations.ToAsyncEnumerable());
        _mocker.Setup<INewsReportRepository, Task<int>>(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid organisationId) => NewsReports.Count(r => r.NewsOrganisationId == organisationId));
        _mocker.Setup<INewsReportRepository, IAsyncEnumerable<NewsReport>>(
                r => r.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Guid organisationId, Expression<Func<NewsReport, bool>> selector) =>
                NewsReports.Where(r => r.NewsOrganisationId == organisationId).Where(selector.Compile())
                    .ToAsyncEnumerable());

        _mocker.Setup<IFilmRepository, IAsyncEnumerable<Film>>(r => r.GetAll())
            .Returns(() => Films.ToAsyncEnumerable());
    }

    /// <summary>The folder the publisher runs in; empty until a test writes files of a previous publish into it.</summary>
    public string OutputDirectory { get; } = Path.Combine(Path.GetTempPath(), "public-db-output-" + Guid.NewGuid());

    public Mock<ILogger<PublicDatabasePublisher>> Logger => _mocker.GetMock<ILogger<PublicDatabasePublisher>>();

    /// <summary>The top-level <c>*.json</c> files in the output folder as they are now, file key to text.</summary>
    public IReadOnlyDictionary<string, string> Files =>
        Directory.EnumerateFiles(OutputDirectory, "*.json").ToDictionary(
            path => Path.GetFileNameWithoutExtension(path)!,
            path => File.ReadAllText(path),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>The exact names of the top-level <c>*.json</c> files in the output folder, sorted.</summary>
    public IReadOnlyList<string> FileNames =>
        Directory.EnumerateFiles(OutputDirectory, "*.json").Select(p => Path.GetFileName(p)!).Order(StringComparer.Ordinal).ToList();

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

    public TvShow GivenTvShow(Action<TvShow>? customize = null) => GivenTvShowFrom(NewPodcast(), customize);

    /// <summary>The TV show a podcast becomes when it is transferred: same id, same name, prefixed file key.</summary>
    public TvShow GivenTvShowFrom(Podcast podcast, Action<TvShow>? customize = null)
    {
        var show = CatalogueParentKindMapper.ToTvShow(podcast);
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

    /// <summary>The news organisation a podcast becomes when it is transferred: same id, prefixed file key.</summary>
    public NewsOrganisation GivenNewsOrganisationFrom(Podcast podcast, Action<NewsOrganisation>? customize = null)
    {
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        customize?.Invoke(organisation);
        NewsOrganisations.Add(organisation);
        return organisation;
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

    /// <summary>The film an episode becomes when it is transferred: the film has the episode's id.</summary>
    public Film GivenFilmFrom(Podcast podcast, Episode episode, Action<Film>? customize = null)
    {
        var film = CatalogueParentKindMapper.ToFilm(podcast, episode);
        customize?.Invoke(film);
        Films.Add(film);
        return film;
    }
    public Film GivenFilm(int releasedDaysAgo = 1, Action<Film>? customize = null)
    {
        var film = CatalogueParentKindMapper.ToFilm(NewPodcast(), NewEpisode(releasedDaysAgo));
        customize?.Invoke(film);
        Films.Add(film);
        return film;
    }

    /// <summary>A file key no item in the scenario has, for a file a previous publish left behind.</summary>
    public string GivenBaselineFileKey() => FileKeyFactory.GetFileKey(_fixture.CreateTitle());

    /// <summary>
    /// Leaves a file in the output folder as a previous publish did: a null id leaves it without one, and
    /// <paramref name="episodeIds"/> become the episodes of a podcast file.
    /// </summary>
    public void GivenExistingFile(string fileKey, Guid? id, params Guid[] episodeIds) =>
        File.WriteAllText(
            Path.Combine(OutputDirectory, fileKey + ".json"),
            id is null
                ? "{}"
                : JsonSerializer.Serialize(new
                {
                    id,
                    name = fileKey,
                    episodes = episodeIds.Select(e => new { id = e })
                }));

    /// <summary>Runs the publisher for a first publish into an empty folder.</summary>
    public Task<PublicationReport> PublishFirstTime() => Publish(new PublishOptions(Initial: true));

    public Task<PublicationReport> Publish(PublishOptions? options = null) =>
        (_publisher ??= _mocker.CreateInstance<PublicDatabasePublisher>()).Run(options ?? new PublishOptions());

    public void Dispose()
    {
        if (Directory.Exists(OutputDirectory))
        {
            Directory.Delete(OutputDirectory, true);
        }
    }
    private Podcast NewPodcast() => _fixture.CreatePodcast(p => p.Name = _fixture.CreateTitle());

    private Episode NewEpisode(int releasedDaysAgo) =>
        _fixture.CreateEpisode(e => e.ReleaseUtc = DomainTestFixture.UtcDaysAgo(releasedDaysAgo));
}
