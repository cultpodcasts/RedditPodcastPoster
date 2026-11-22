using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.Films;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using System.Text.Json;
using Xunit;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;

namespace FunctionHost.Tests.Api.Services.Episodes;

/// <summary>
/// RPP #1522 Move to an existing film: <c>POST episode/{podcastId}/{episodeId}/film</c> with <c>filmId</c> merges the
/// episode's links into that film (the Manic case: a re-upload of a film already in the catalogue).
/// </summary>
public class EpisodeFilmMergeIntoExistingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly Dictionary<Guid, (string Json, int Version)> _films = [];
    private Action? _beforeReplace;
    private int _filmSaves;
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private readonly List<Guid> _searchDeletes = [];
    private bool _searchUploadSucceeds = true;
    private bool _searchDeleteSucceeds = true;
    private readonly List<(Guid EpisodeId, string Path)> _redirects = [];

    public EpisodeFilmMergeIntoExistingRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        var films = _mocker.GetMock<IFilmRepository>();
        films.Setup(r => r.GetFilm(It.IsAny<Guid>())).ReturnsAsync((Guid id) => Stored(id));
        films
            .Setup(r => r.GetFilmForUpdate(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => Stored(id) is { } film
                ? new FilmVersion(film, _films[id].Version.ToString())
                : null);
        films
            .Setup(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()))
            .ReturnsAsync((Film film, string etag) =>
            {
                var hook = _beforeReplace;
                _beforeReplace = null;
                hook?.Invoke();
                if (!_films.TryGetValue(film.Id, out var stored) || stored.Version.ToString() != etag)
                {
                    return false;
                }

                _filmSaves++;
                _films[film.Id] = (JsonSerializer.Serialize(film), stored.Version + 1);
                return true;
            });
        films
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) =>
                _films.Keys.Select(id => Stored(id)!).Where(selector.Compile()).ToAsyncEnumerable());
        var search = _mocker.GetMock<IPlayableSearchDocumentSwap>();
        search
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploads.Add(documents))
            .ReturnsAsync(() => _searchUploadSucceeds);
        search
            .Setup(s => s.DeleteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => _searchDeletes.AddRange(ids))
            .ReturnsAsync(() => _searchDeleteSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Redirect(It.IsAny<PodcastEpisode>(), It.IsAny<string>()))
            .Callback<PodcastEpisode, string>((target, path) => _redirects.Add((target.Episode.Id, path)))
            .ReturnsAsync(new WriteResult(true));
        _mocker.GetMock<ILogger<EpisodeFilmMoveService>>();
    }

    private Film? Stored(Guid id) =>
        _films.TryGetValue(id, out var stored) ? JsonSerializer.Deserialize<Film>(stored.Json) : null;

    private Film SeedFilm(Action<Film>? configure = null)
    {
        var film = new Film("Manic") { Id = _fixture.CreateGuid(), Description = "MANIC chronicles…" };
        configure?.Invoke(film);
        _films[film.Id] = (JsonSerializer.Serialize(film), 1);
        return film;
    }

    private static void SetYouTube(Film film, string videoId)
    {
        film.Services ??= [];
        film.Services[ServiceKeys.YouTube] = new ServiceLink { Url = new Uri($"https://www.youtube.com/watch?v={videoId}") };
        film.YouTubeId = videoId;
    }

    private Task<EpisodeFilmMoveResult> Merge(Guid podcastId, Guid episodeId, Guid filmId, string? title = null) =>
        _mocker.CreateInstance<EpisodeFilmMoveService>().MoveAsync(
            new EpisodeFilmMoveCommand(podcastId, episodeId, new EpisodeFilmMoveRequest { FilmId = filmId, Title = title }),
            CancellationToken.None);

    private (Podcast Podcast, Episode Episode) SeedYouTubeEpisode()
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        return (podcast, episode);
    }

    [Fact(DisplayName =
        "Merging a YouTube episode into an existing film with no YouTube link adds the link and compact id to that film, keeps the film's id, name and identity, pushes the film's search document, deletes the episode's document and row, and points the short link at the film page.")]
    public async Task merge_adds_youtube_link_to_existing_film_and_retires_episode()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var film = SeedFilm(f => { f.ImdbId = "tt6089644"; f.TmdbId = 456727; });
        var episodeUrl = episode.Services![ServiceKeys.YouTube].Url!;

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id, title: "ignored when filmId is set");

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.FilmId.Should().Be(film.Id);
        result.Name.Should().Be("Manic");
        var stored = Stored(film.Id)!;
        stored.Name.Should().Be("Manic");
        stored.ImdbId.Should().Be("tt6089644");
        stored.Services![ServiceKeys.YouTube].Url.Should().Be(episodeUrl);
        stored.YouTubeId.Should().Be(EpisodeServicePresence.YouTubeEpisodeId(episode));
        _uploads.Should().ContainSingle().Which.Should().ContainSingle().Which.Id.Should().Be(film.Id.ToString());
        _searchDeletes.Should().Equal(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        _redirects.Should().ContainSingle().Which.Should().Be(
            (episode.Id, CatalogueShortId.PlayablePath("Manic", film.Id, CatalogueShortId.Film)));
        _mocker.GetMock<IFilmRepository>().Verify(r => r.Save(It.IsAny<Film>()), Times.Never);
    }

    [Fact(DisplayName =
        "The Manic case (RPP #1411): merging an episode whose YouTube video differs from the film's primary YouTube adds that upload as an additional YouTube link, keeps the film's primary YouTube, pushes search, and retires the episode.")]
    public async Task merge_with_a_different_youtube_link_adds_an_alternate_and_retires_episode()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var film = SeedFilm(f => SetYouTube(f, "VCRf_qazO9s"));
        var episodeUrl = episode.Services![ServiceKeys.YouTube].Url!;
        var episodeYouTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode);

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.FilmId.Should().Be(film.Id);
        var stored = Stored(film.Id)!;
        stored.YouTubeId.Should().Be("VCRf_qazO9s");
        stored.Services![ServiceKeys.YouTube].Url.Should().Be(new Uri("https://www.youtube.com/watch?v=VCRf_qazO9s"));
        stored.AdditionalYouTube.Should().ContainSingle();
        stored.AdditionalYouTube![0].YouTubeId.Should().Be(episodeYouTubeId);
        stored.AdditionalYouTube[0].Url.Should().Be(episodeUrl);
        stored.AdditionalYouTube[0].SourceEpisodeId.Should().Be(episode.Id);
        _uploads.Should().ContainSingle().Which.Should().ContainSingle();
        _uploads[0][0].YouTubeAlternates.Should().ContainSingle().Which.Should().StartWith(episodeYouTubeId + "|");
        _searchDeletes.Should().Equal(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        _redirects.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Merging an episode that has a different Spotify link from the film is still a 409 and writes nothing (Spotify stays single-valued).")]
    public async Task merge_with_a_different_spotify_link_conflicts_and_writes_nothing()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        episode.Services![ServiceKeys.Spotify] = new ServiceLink { Url = new Uri("https://open.spotify.com/episode/sourceep") };
        _episodes.Seed(episode);
        var film = SeedFilm(f =>
        {
            SetYouTube(f, "VCRf_qazO9s");
            f.Services![ServiceKeys.Spotify] = new ServiceLink { Url = new Uri("https://open.spotify.com/episode/filmep") };
        });

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Conflict);
        result.FilmId.Should().Be(film.Id);
        result.Error.Should().Contain("spotify");
        result.Error.Should().NotContain("#1411");
        _filmSaves.Should().Be(0);
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "An episode that is an exact duplicate (the film already has the same YouTube video, written as a youtu.be link) merges without changing the film's links and still retires the episode.")]
    public async Task merge_of_an_exact_duplicate_retires_episode_without_changing_links()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var videoId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
        var film = SeedFilm(f =>
        {
            f.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri($"https://youtu.be/{videoId}") }
            };
            f.YouTubeId = videoId;
        });
        episode.Subjects = [];
        _episodes.Seed(episode);

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        _filmSaves.Should().Be(0);
        Stored(film.Id)!.Services![ServiceKeys.YouTube].Url.Should().Be(new Uri($"https://youtu.be/{videoId}"));
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Merging adds the episode's subjects the film lacks, but not ones the curator removed from the film.")]
    public async Task merge_adds_missing_subjects_but_not_removed_ones()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        episode.Subjects = ["The Significants", "Herbalife", "Kalina Bertin"];
        _episodes.Seed(episode);
        var film = SeedFilm(f =>
        {
            f.Subjects = ["The Significants"];
            f.RemovedSubjects = ["Herbalife"];
        });

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        Stored(film.Id)!.Subjects.Should().Equal("The Significants", "Kalina Bertin");
    }

    [Fact(DisplayName =
        "A film edit that lands between the merge's read and its save is not overwritten: the merge re-reads the film and adds the link to the latest version.")]
    public async Task merge_retries_on_etag_conflict_and_keeps_concurrent_edit()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var film = SeedFilm();
        _beforeReplace = () =>
        {
            var concurrent = Stored(film.Id)!;
            concurrent.Description = "edited concurrently";
            _films[film.Id] = (JsonSerializer.Serialize(concurrent), _films[film.Id].Version + 1);
        };

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        var stored = Stored(film.Id)!;
        stored.Description.Should().Be("edited concurrently");
        stored.Services.Should().ContainKey(ServiceKeys.YouTube);
    }

    [Fact(DisplayName =
        "When the episode's search document cannot be deleted the merge stops with failureIndexing and keeps the episode row, and a re-send then finishes it without adding the link twice.")]
    public async Task merge_search_delete_failure_keeps_episode_and_resend_resumes()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var film = SeedFilm();
        _searchDeleteSucceeds = false;

        // Act
        var first = await Merge(podcast.Id, episode.Id, film.Id);
        _searchDeleteSucceeds = true;
        var second = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        first.Status.Should().Be(EpisodeFilmMoveStatus.Failed);
        first.FailureIndexing.Should().BeTrue();
        second.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        _filmSaves.Should().Be(1);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Merge refuses a missing film (404), a removed film (400), a removed episode (400), and the film made from this same episode (400), writing nothing.")]
    public async Task merge_refuses_bad_targets()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var removedFilm = SeedFilm(f => f.Removed = true);
        var (removedPodcast, removedEpisode) = SeedYouTubeEpisode();
        removedEpisode.Removed = true;
        _episodes.Seed(removedEpisode);
        var film = SeedFilm();

        // Act
        var missing = await Merge(podcast.Id, episode.Id, Guid.NewGuid());
        var removed = await Merge(podcast.Id, episode.Id, removedFilm.Id);
        var removedSource = await Merge(removedPodcast.Id, removedEpisode.Id, film.Id);
        var self = await Merge(podcast.Id, episode.Id, episode.Id);

        // Assert
        missing.Status.Should().Be(EpisodeFilmMoveStatus.NotFound);
        removed.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        removedSource.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        self.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        _filmSaves.Should().Be(0);
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Theory(DisplayName =
        "Merge refuses a held or rejected episode (400), writing nothing, because a split-off stays hidden until a curator approves it.")]
    [InlineData(ApprovalStatus.AwaitingApproval)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task merge_refuses_held_episode(ApprovalStatus status)
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        episode.ApprovalStatus = status;
        _episodes.Seed(episode);
        var film = SeedFilm();

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        result.Error.Should().Be("An episode held for approval cannot be moved to a film");
        _filmSaves.Should().Be(0);
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A re-send after the merge finished (episode row gone) is a 404, not a second write.")]
    public async Task merge_resend_after_completion_is_not_found()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var film = SeedFilm();
        (await Merge(podcast.Id, episode.Id, film.Id)).Status.Should().Be(EpisodeFilmMoveStatus.Accepted);

        // Act
        var again = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        again.Status.Should().Be(EpisodeFilmMoveStatus.NotFound);
        _filmSaves.Should().Be(1);
    }

    [Fact(DisplayName =
        "Merging materialises the episode's Spotify / Apple / YouTube ids into links first, as a move to a new film does.")]
    public async Task merge_materialises_service_links_first()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var film = SeedFilm();

        // Act
        await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        _mocker.GetMock<IEpisodeServiceMaterialiser>().Verify(
            m => m.MaterialiseAsync(It.IsAny<Podcast>(), It.Is<Episode>(e => e.Id == episode.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [Fact(DisplayName =
        "Merging into an existing film is a conflict naming the holder when the episode's YouTube id already belongs to another item (RPP #1411).")]
    public async Task merge_is_conflict_when_another_item_holds_the_youtube_id()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var film = SeedFilm();
        var holderId = _fixture.CreateGuid();
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindYouTubeHolderExcluding(
                youTubeId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner("NewsReport", "Outlet", holderId));

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Conflict);
        result.Error.Should().Contain(youTubeId);
        result.Error.Should().Contain("NewsReport");
        result.Error.Should().Contain(holderId.ToString());
        _filmSaves.Should().Be(0);
        _episodes.GetStored(episode.Id).Should().NotBeNull();
    }


    [Fact(DisplayName =
        "Merging is a conflict naming the other film when the target already holds the source YouTube id and a second film also holds it, because uniqueness still checks the already-held id with the target excluded (RPP #1411).")]
    public async Task merge_is_conflict_when_another_film_holds_the_youtube_id_even_if_the_target_already_has_it()
    {
        // Arrange
        var (podcast, episode) = SeedYouTubeEpisode();
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
        var film = SeedFilm(f => SetYouTube(f, youTubeId));
        var other = new Film("Other Film") { Id = _fixture.CreateGuid() };
        SetYouTube(other, youTubeId);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> predicate) =>
                new[] { Stored(film.Id)!, other }.FirstOrDefault(predicate.Compile()));
        _mocker.Use(Options.Create(new SubmitContentTypesOptions()));
        _mocker.Use<IUrlMembershipLookup>(_mocker.CreateInstance<UrlMembershipLookup>());

        // Act
        var result = await Merge(podcast.Id, episode.Id, film.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Conflict);
        result.Error.Should().Contain(youTubeId);
        result.Error.Should().Contain("Film");
        result.Error.Should().Contain(other.Id.ToString());
        _filmSaves.Should().Be(0);
        _episodes.GetStored(episode.Id).Should().NotBeNull();
    }
}
