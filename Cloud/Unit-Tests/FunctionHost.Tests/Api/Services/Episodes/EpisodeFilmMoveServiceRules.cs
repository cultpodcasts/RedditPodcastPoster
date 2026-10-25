using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

public class EpisodeFilmMoveServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly List<Film> _films = [];
    private readonly List<Film> _savedFilms = [];
    private IReadOnlyList<EpisodeSearchRecord>? _uploadedSearchDocuments;
    private bool _searchUploadSucceeds = true;
    private readonly List<Guid> _deletedShortLinks = [];
    private Func<DeleteResult> _shortLinkDelete = () => new DeleteResult(true);

    public EpisodeFilmMoveServiceRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        var films = _mocker.GetMock<IFilmRepository>();
        films
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _films.SingleOrDefault(film => film.Id == id));
        films
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => _films.Where(selector.Compile()).ToAsyncEnumerable());
        films
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film =>
            {
                _savedFilms.Add(film);
                _films.Add(film);
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>(
                (documents, _) => _uploadedSearchDocuments = documents)
            .ReturnsAsync(() => _searchUploadSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _deletedShortLinks.Add(target.Episode.Id))
            .ReturnsAsync(() => _shortLinkDelete());
        _mocker.GetMock<ILogger<EpisodeFilmMoveService>>();
    }

    private Task<EpisodeFilmMoveResult> Move(Guid podcastId, Guid episodeId, string? title = null) =>
        _mocker.CreateInstance<EpisodeFilmMoveService>().MoveAsync(
            new EpisodeFilmMoveCommand(podcastId, episodeId, new EpisodeFilmMoveRequest { Title = title }),
            CancellationToken.None);

    [Fact(DisplayName =
        "Moving an episode to a film saves a film with the episode id, title, services and posted flags, then deletes only that episode row.")]
    public async Task move_saves_film_with_episode_id_and_deletes_episode_row()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Posted = true;
        episode.Tweeted = true;
        var sibling = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode, sibling);

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.FilmId.Should().Be(episode.Id);
        result.Name.Should().Be(episode.Title.Trim());
        var film = _savedFilms.Should().ContainSingle().Subject;
        film.Id.Should().Be(episode.Id);
        film.Name.Should().Be(episode.Title.Trim());
        film.Posted.Should().BeTrue();
        film.Tweeted.Should().BeTrue();
        film.Services.Should().BeEquivalentTo(episode.Services);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, sibling.Id)).Should().NotBeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Moving an episode that has ids.youtube but no services.youtube.url materialises a watch URL on the film, because films keep Services only and would otherwise lose YouTube.")]
    public async Task move_materialises_youtube_watch_url_from_ids_when_services_url_missing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, null, null);
        var youTubeId = episode.Ids!.YouTube!;
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        var film = _savedFilms.Should().ContainSingle().Subject;
        film.Services.Should().ContainKey(ServiceKeys.YouTube);
        film.Services![ServiceKeys.YouTube].Url.Should().Be(_fixture.DefaultYouTubeUrl(youTubeId));
    }

    [Fact(DisplayName =
        "Moving the last episode leaves the source podcast in place, because only the episode moves and the podcast keeps its feed settings.")]
    public async Task move_of_last_episode_keeps_podcast()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
        (await _episodes.Count(podcast.Id)).Should().Be(0);
    }

    [Fact(DisplayName =
        "An active episode moved out of a removed podcast becomes an active film, because the episode, not the podcast, is what moves.")]
    public async Task film_from_removed_podcast_is_active()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        _savedFilms.Should().ContainSingle().Which.IsRemoved().Should().BeFalse();
    }

    [Fact(DisplayName =
        "A title in the request names the film and its search document instead of the episode title.")]
    public async Task title_override_names_film_and_search_document()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var title = _fixture.CreateTitle();

        // Act
        var result = await Move(podcast.Id, episode.Id, "  " + title + "  ");

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.Name.Should().Be(title);
        _savedFilms.Should().ContainSingle().Which.Name.Should().Be(title);
        _uploadedSearchDocuments.Should().ContainSingle().Which.Title.Should().Be(title);
    }

    [Fact(DisplayName =
        "The search document keeps the episode guid and switches ContentKind to Film with no series name, as the catalogue migrator does.")]
    public async Task search_swap_uses_same_guid_and_film_kind()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        await Move(podcast.Id, episode.Id);

        // Assert
        var document = _uploadedSearchDocuments.Should().ContainSingle().Subject;
        document.Id.Should().Be(episode.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.Film);
        document.SeriesName.Should().BeNull();
    }

    [Fact(DisplayName =
        "When another film already has the name, the move is a conflict and nothing is written, because film names are unique case-insensitively.")]
    public async Task name_clash_is_conflict()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var existing = CatalogueParentKindMapper.ToFilm(podcast, _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast));
        existing.Name = episode.Title.Trim().ToUpperInvariant();
        _films.Add(existing);

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Conflict);
        result.FilmId.Should().Be(existing.Id);
        _savedFilms.Should().BeEmpty();
        _uploadedSearchDocuments.Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A title containing '/' is a bad request and nothing is written, because the film route uses the name as one path segment.")]
    public async Task slash_in_title_is_bad_request()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        var result = await Move(podcast.Id, episode.Id, _fixture.CreateTitle() + "/" + _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        _savedFilms.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A removed episode cannot be moved to a film, because the migrator only moves active episodes, and the search swap receives no upload.")]
    public async Task removed_episode_is_bad_request()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Removed = true;
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        _savedFilms.Should().BeEmpty();
        _uploadedSearchDocuments.Should().BeNull();
    }

    [Fact(DisplayName =
        "An unknown podcast or an episode not in that podcast is not found.")]
    public async Task unknown_episode_is_not_found()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);

        // Act
        var missingEpisode = await Move(podcast.Id, _fixture.CreateGuid());
        var missingPodcast = await Move(_fixture.CreateGuid(), _fixture.CreateGuid());

        // Assert
        missingEpisode.Status.Should().Be(EpisodeFilmMoveStatus.NotFound);
        missingPodcast.Status.Should().Be(EpisodeFilmMoveStatus.NotFound);
    }

    [Fact(DisplayName =
        "When the search swap fails, the episode row is kept and the result is Failed, so the source is never lost before search points at the film.")]
    public async Task search_failure_keeps_episode_row()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _searchUploadSucceeds = false;

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Re-sending after a failed search swap resumes: the film with the episode id is reused, search is swapped and the episode row is deleted.")]
    public async Task resend_after_search_failure_resumes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var title = _fixture.CreateTitle();
        _searchUploadSucceeds = false;
        await Move(podcast.Id, episode.Id, title);
        _searchUploadSucceeds = true;

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.Name.Should().Be(title);
        _savedFilms.Should().ContainSingle();
        _uploadedSearchDocuments.Should().ContainSingle().Which.Title.Should().Be(title);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "The search document is the same one the film edit and the film pull indexer write, so the next pull run does not rewrite it.")]
    public async Task search_document_matches_film_edit_shape()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        await Move(podcast.Id, episode.Id);

        // Assert
        var film = _savedFilms.Should().ContainSingle().Subject;
        _uploadedSearchDocuments.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(CataloguePlayableSearchDocument.For(film));
    }

    [Fact(DisplayName =
        "After a move the episode's short-link record is deleted, so the Shortner finds the film in search and redirects the old short link straight to the film page.")]
    public async Task move_deletes_short_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);

        // Act
        await Move(podcast.Id, episode.Id);

        // Assert
        _deletedShortLinks.Should().ContainSingle().Which.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When the search swap fails the short-link record is kept, because the old link must still reach the episode that has not moved yet.")]
    public async Task search_failure_keeps_short_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _searchUploadSucceeds = false;

        // Act
        await Move(podcast.Id, episode.Id);

        // Assert
        _deletedShortLinks.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A short-link delete failure does not fail the move, because the film, search and episode row are already done.")]
    public async Task short_link_failure_still_accepts()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _shortLinkDelete = () => throw new InvalidOperationException("synthetic KV failure");

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-sending after a completed move is accepted for the same film without writing again, so a double click or a lost response is not an error.")]
    public async Task resend_after_completed_move_is_accepted()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var title = _fixture.CreateTitle();
        await Move(podcast.Id, episode.Id, title);
        _uploadedSearchDocuments = null;

        // Act
        var result = await Move(podcast.Id, episode.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.FilmId.Should().Be(episode.Id);
        result.Name.Should().Be(title);
        _savedFilms.Should().ContainSingle();
        _uploadedSearchDocuments.Should().BeNull();
    }
}
