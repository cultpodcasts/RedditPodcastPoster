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
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

public class EpisodeTvShowMoveServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly List<TvShow> _shows = [];
    private readonly List<TvShow> _savedShows = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<TvShowEpisode> _savedTvEpisodes = [];
    private readonly List<Film> _films = [];
    private readonly List<(Guid ShowId, CatalogueParentIndexerPatch Patch)> _patches = [];
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private bool _searchUploadSucceeds = true;
    private Action? _afterSearchUpload;
    private readonly List<(Guid EpisodeId, string Path)> _redirects = [];
    private Func<WriteResult> _redirectResult = () => new WriteResult(true);

    public EpisodeTvShowMoveServiceRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        var shows = _mocker.GetMock<ITvShowRepository>();
        shows
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.SingleOrDefault(show => show.Id == id));
        shows
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector) => _shows.Where(selector.Compile()).ToAsyncEnumerable());
        shows
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show =>
            {
                _savedShows.Add(show);
                _shows.Add(show);
            })
            .Returns(Task.CompletedTask);
        shows
            .Setup(r => r.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .Callback<Guid, CatalogueParentIndexerPatch>((id, patch) => _patches.Add((id, patch)))
            .ReturnsAsync((Guid id, CatalogueParentIndexerPatch _) => _shows.Any(show => show.Id == id));
        var tvEpisodes = _mocker.GetMock<ITvShowEpisodeRepository>();
        tvEpisodes
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> selector) =>
                _tvEpisodes.FirstOrDefault(selector.Compile()));
        tvEpisodes
            .Setup(r => r.GetTvShowEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid showId, Guid id) =>
                _tvEpisodes.SingleOrDefault(item => item.TvShowId == showId && item.Id == id));
        tvEpisodes
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(item =>
            {
                _savedTvEpisodes.Add(item);
                _tvEpisodes.Add(item);
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _films.SingleOrDefault(film => film.Id == id));
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) =>
            {
                _uploads.Add(documents);
                _afterSearchUpload?.Invoke();
            })
            .ReturnsAsync(() => _searchUploadSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Redirect(It.IsAny<PodcastEpisode>(), It.IsAny<string>()))
            .Callback<PodcastEpisode, string>((target, path) => _redirects.Add((target.Episode.Id, path)))
            .ReturnsAsync(() => _redirectResult());
        _mocker.GetMock<ILogger<EpisodeTvShowMoveService>>();
    }

    private TvShow SeedShow(Action<TvShow>? configure = null)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(show);
        _shows.Add(show);
        return show;
    }

    private (RedditPodcastPoster.Models.Podcasts.Podcast Podcast, Episode Episode) SeedEpisode(
        Action<Episode>? configure = null)
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        configure?.Invoke(episode);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        return (podcast, episode);
    }

    private Task<EpisodeTvShowMoveResult> Move(Guid podcastId, Guid episodeId, EpisodeTvShowMoveRequest request) =>
        _mocker.CreateInstance<EpisodeTvShowMoveService>().MoveAsync(
            new EpisodeTvShowMoveCommand(podcastId, episodeId, request),
            CancellationToken.None);

    private Task<EpisodeTvShowMoveResult> MoveToShow(Guid podcastId, Guid episodeId, Guid showId, string? title = null) =>
        Move(podcastId, episodeId, new EpisodeTvShowMoveRequest { TvShowId = showId, Title = title });

    [Fact(DisplayName =
        "Moving an episode into an existing TV show saves a TV episode with the episode id, the show's id and name, services and posted flags, then deletes only that episode row and keeps the podcast.")]
    public async Task move_to_existing_show_saves_tv_episode_and_deletes_episode_row()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode(item =>
        {
            item.Posted = true;
            item.Tweeted = true;
        });
        var sibling = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _episodes.Seed(sibling);

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.TvShowId.Should().Be(show.Id);
        result.TvShowName.Should().Be(show.Name);
        result.EpisodeId.Should().Be(episode.Id);
        result.CreatedShow.Should().BeFalse();
        var tvEpisode = _savedTvEpisodes.Should().ContainSingle().Subject;
        tvEpisode.Id.Should().Be(episode.Id);
        tvEpisode.TvShowId.Should().Be(show.Id);
        tvEpisode.TvShowName.Should().Be(show.Name);
        tvEpisode.Title.Should().Be(episode.Title);
        tvEpisode.Posted.Should().BeTrue();
        tvEpisode.Tweeted.Should().BeTrue();
        tvEpisode.Services.Should().BeEquivalentTo(episode.Services);
        _savedShows.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, sibling.Id)).Should().NotBeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A TV show name without createShow attaches to the one show with that name, case-insensitively, so the TV-show picker can send the suggested name.")]
    public async Task name_attaches_to_existing_show_case_insensitively()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await Move(podcast.Id, episode.Id,
            new EpisodeTvShowMoveRequest { TvShowName = $"  {show.Name.ToUpperInvariant()} " });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.TvShowId.Should().Be(show.Id);
        _savedTvEpisodes.Should().ContainSingle().Which.TvShowId.Should().Be(show.Id);
        _savedShows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A TV show name that no show has, without createShow, is not found and nothing is written, so a typo never creates a show.")]
    public async Task unknown_name_without_create_is_not_found()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await Move(podcast.Id, episode.Id,
            new EpisodeTvShowMoveRequest { TvShowName = _fixture.CreateTitle() });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.NotFound);
        _savedShows.Should().BeEmpty();
        _savedTvEpisodes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "createShow with a new name creates a show with a fresh id (not the podcast id), the publisher and no indexing, so it never claims the podcast's YouTube channel.")]
    public async Task create_show_has_fresh_id_publisher_and_no_indexing()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        podcast.YouTubeChannelId = _fixture.Create<string>();
        var name = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();

        // Act
        var result = await Move(podcast.Id, episode.Id,
            new EpisodeTvShowMoveRequest { TvShowName = name, CreateShow = true, Publisher = publisher });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.CreatedShow.Should().BeTrue();
        var show = _savedShows.Should().ContainSingle().Subject;
        show.Id.Should().NotBe(podcast.Id);
        show.Id.Should().NotBe(Guid.Empty);
        show.Name.Should().Be(name);
        show.PublisherName.Should().Be(publisher);
        show.Indexing.Should().BeNull();
        show.FileKey.Should().Be(new TvShow(name).FileKey);
        show.LatestReleased.Should().Be(_savedTvEpisodes.Single().ReleaseSort);
        result.TvShowId.Should().Be(show.Id);
        _savedTvEpisodes.Should().ContainSingle().Which.TvShowId.Should().Be(show.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "createShow with a name one show already has joins that show and does not change its publisher, so a re-send after an interrupted create never makes a second show.")]
    public async Task create_show_with_existing_name_joins_show()
    {
        // Arrange
        var publisher = _fixture.Create<string>();
        var show = SeedShow(item => item.PublisherName = publisher);
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await Move(podcast.Id, episode.Id, new EpisodeTvShowMoveRequest
        {
            TvShowName = show.Name, CreateShow = true, Publisher = _fixture.Create<string>()
        });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.CreatedShow.Should().BeFalse();
        result.TvShowId.Should().Be(show.Id);
        _savedShows.Should().BeEmpty();
        show.PublisherName.Should().Be(publisher);
    }

    [Fact(DisplayName =
        "When several TV shows have the name, the move is a conflict and nothing is written, because the target would be a guess.")]
    public async Task several_shows_with_name_is_conflict()
    {
        // Arrange
        var show = SeedShow();
        SeedShow(item => item.Name = show.Name.ToUpperInvariant());
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await Move(podcast.Id, episode.Id,
            new EpisodeTvShowMoveRequest { TvShowName = show.Name, CreateShow = true });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        _savedShows.Should().BeEmpty();
        _savedTvEpisodes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    public static TheoryData<string, Guid?, string?, bool, string?> InvalidRequests()
    {
        var fixture = new DomainTestFixture();
        var name = fixture.CreateTitle();
        return new TheoryData<string, Guid?, string?, bool, string?>
        {
            { "neither id nor name", null, null, false, null },
            { "both id and name", fixture.CreateGuid(), name, false, null },
            { "createShow with an id", fixture.CreateGuid(), null, true, null },
            { "publisher without createShow", null, name, false, fixture.Create<string>() },
            { "'/' in the show name", null, $"{name}/{fixture.CreateTitle()}", true, null }
        };
    }

    [Theory(DisplayName =
        "An invalid target is a bad request and nothing is read or written: exactly one of tvShowId or tvShowName, createShow only by name, publisher only with createShow, and no '/' in a show name (the TV route uses it as one path segment).")]
    [MemberData(nameof(InvalidRequests))]
    public async Task invalid_target_is_bad_request(
        string _, Guid? showId, string? showName, bool createShow, string? publisher)
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await Move(podcast.Id, episode.Id, new EpisodeTvShowMoveRequest
        {
            TvShowId = showId, TvShowName = showName, CreateShow = createShow, Publisher = publisher
        });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        _savedShows.Should().BeEmpty();
        _savedTvEpisodes.Should().BeEmpty();
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A removed TV show cannot take episodes, by id or by name, because the episode would vanish from the site with it.")]
    public async Task removed_show_is_bad_request()
    {
        // Arrange
        var show = SeedShow(item => item.Removed = true);
        var (podcast, episode) = SeedEpisode();

        // Act
        var byId = await MoveToShow(podcast.Id, episode.Id, show.Id);
        var byName = await Move(podcast.Id, episode.Id, new EpisodeTvShowMoveRequest { TvShowName = show.Name });

        // Assert
        byId.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        byName.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        _savedTvEpisodes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "An unknown podcast, an episode not in that podcast, or an unknown TV show id is not found.")]
    public async Task unknown_podcast_episode_or_show_is_not_found()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        var unknownPodcast = await MoveToShow(_fixture.CreateGuid(), episode.Id, show.Id);
        var unknownEpisode = await MoveToShow(podcast.Id, _fixture.CreateGuid(), show.Id);
        var unknownShow = await MoveToShow(podcast.Id, episode.Id, _fixture.CreateGuid());

        // Assert
        unknownPodcast.Status.Should().Be(EpisodeTvShowMoveStatus.NotFound);
        unknownEpisode.Status.Should().Be(EpisodeTvShowMoveStatus.NotFound);
        unknownShow.Status.Should().Be(EpisodeTvShowMoveStatus.NotFound);
        _savedTvEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A removed episode cannot be moved to a TV show, because the migrator only moves active episodes.")]
    public async Task removed_episode_is_bad_request()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode(item => item.Removed = true);

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        _savedTvEpisodes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A title in the request names the TV episode and its search document instead of the episode title.")]
    public async Task title_override_names_tv_episode_and_search_document()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        var title = _fixture.CreateTitle();

        // Act
        await MoveToShow(podcast.Id, episode.Id, show.Id, $" {title} ");

        // Assert
        _savedTvEpisodes.Should().ContainSingle().Which.Title.Should().Be(title);
        _uploads.Should().ContainSingle().Which.Should().ContainSingle().Which.Title.Should().Be(title);
    }

    [Fact(DisplayName =
        "The search document keeps the episode guid, switches ContentKind to TvShowEpisode with the show as series name, and is the shape the TV episode edit writes, so the pull indexer does not rewrite it.")]
    public async Task search_swap_uses_same_guid_and_tv_shape()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        var document = _uploads.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        document.Id.Should().Be(episode.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
        document.SeriesName.Should().Be(show.Name);
        document.Should().BeEquivalentTo(CataloguePlayableSearchDocument.For(_savedTvEpisodes.Single()));
    }

    [Fact(DisplayName =
        "When the search swap fails, the episode row and its short link are kept and the result is Failed with failureIndexing, so the source is never lost before search points at the TV episode.")]
    public async Task search_failure_keeps_episode_row()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _searchUploadSucceeds = false;

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _redirects.Should().BeEmpty();
        _patches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Re-sending after a failed search swap resumes: the TV episode with the episode id is reused, not saved again, then search is swapped and the episode row is deleted.")]
    public async Task resend_after_search_failure_resumes()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _searchUploadSucceeds = false;
        await MoveToShow(podcast.Id, episode.Id, show.Id);
        _searchUploadSucceeds = true;

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        _savedTvEpisodes.Should().ContainSingle();
        _uploads.Should().HaveCount(2);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-sending a create after a failed search swap joins the show the first attempt created, so an interrupted move never makes two shows.")]
    public async Task resend_create_after_search_failure_reuses_created_show()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var request = new EpisodeTvShowMoveRequest { TvShowName = _fixture.CreateTitle(), CreateShow = true };
        _searchUploadSucceeds = false;
        var first = await Move(podcast.Id, episode.Id, request);
        _searchUploadSucceeds = true;

        // Act
        var result = await Move(podcast.Id, episode.Id, request);

        // Assert
        first.CreatedShow.Should().BeTrue();
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.TvShowId.Should().Be(first.TvShowId);
        _savedShows.Should().ContainSingle();
        _savedTvEpisodes.Should().ContainSingle();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-sending after a completed move is accepted for the same show without writing again, so a double click or a lost response is not an error.")]
    public async Task resend_after_completed_move_is_accepted()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.TvShowId.Should().Be(show.Id);
        result.EpisodeId.Should().Be(episode.Id);
        _savedTvEpisodes.Should().ContainSingle();
        _uploads.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Re-sending a completed move with a different show is a conflict naming the show the episode is in, because a move never silently re-targets.")]
    public async Task resend_to_other_show_after_completed_move_is_conflict()
    {
        // Arrange
        var show = SeedShow();
        var other = SeedShow();
        var (podcast, episode) = SeedEpisode();
        await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, other.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        result.TvShowId.Should().Be(show.Id);
        _savedTvEpisodes.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the episode id is already a TV episode of another show while the episode row exists, the move is a conflict and the episode row is kept.")]
    public async Task tv_episode_in_other_show_is_conflict()
    {
        // Arrange
        var show = SeedShow();
        var other = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _tvEpisodes.Add(CatalogueParentKindMapper.ToTvShowEpisode(episode, other));

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        result.TvShowId.Should().Be(other.Id);
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When a film already has the episode id (an interrupted move to film), the move is a conflict and nothing is written, because one id cannot be both.")]
    public async Task film_with_episode_id_is_conflict()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _films.Add(CatalogueParentKindMapper.ToFilm(podcast, episode));

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        _savedTvEpisodes.Should().BeEmpty();
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the show or the new TV episode is deleted after the search swap, the episode row is kept and the result is Failed, because deleting it then would lose the episode.")]
    public async Task concurrent_delete_before_episode_delete_keeps_episode_row()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _afterSearchUpload = () => _tvEpisodes.RemoveAll(item => item.Id == episode.Id);

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _redirects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A show deleted after the search swap stops the move with the episode row kept.")]
    public async Task concurrent_show_delete_keeps_episode_row()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _afterSearchUpload = () => _shows.Remove(show);

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Failed);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A move of an episode newer than the show's latest release patches only latestReleased on the show, so a concurrent curator edit of the show is never overwritten.")]
    public async Task newer_episode_patches_latest_released_only()
    {
        // Arrange
        var show = SeedShow(item => item.LatestReleased = DomainTestFixture.UtcDaysAgo(30));
        var (podcast, episode) = SeedEpisode(item => item.ReleaseUtc = DomainTestFixture.UtcDaysAgo(1));

        // Act
        await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        var (showId, patch) = _patches.Should().ContainSingle().Subject;
        showId.Should().Be(show.Id);
        patch.LatestReleased.Should().Be(_savedTvEpisodes.Single().ReleaseSort);
        patch.LastIndexed.Should().BeNull();
        patch.ChangedIndexingProperties.Should().BeEmpty();
        _savedShows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A move of an episode older than the show's latest release leaves the show untouched.")]
    public async Task older_episode_does_not_patch_show()
    {
        // Arrange
        var show = SeedShow(item => item.LatestReleased = DomainTestFixture.UtcDaysAgo(1));
        var (podcast, episode) = SeedEpisode(item => item.ReleaseUtc = DomainTestFixture.UtcDaysAgo(30));

        // Act
        await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        _patches.Should().BeEmpty();
        _savedShows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "After a move the episode's short link points at the TV episode page /tv/{show}/{prefixed short id}, so old episode links open the TV episode.")]
    public async Task move_redirects_short_link_to_tv_page()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        var redirect = _redirects.Should().ContainSingle().Subject;
        redirect.EpisodeId.Should().Be(episode.Id);
        redirect.Path.Should().Be(
            CatalogueShortId.PlayablePath(show.Name, episode.Id, CatalogueShortId.TvShowEpisode));
        redirect.Path.Should().StartWith("/tv/");
    }

    [Fact(DisplayName =
        "A short-link redirect failure does not fail the move, because the TV episode, search and episode row are already done.")]
    public async Task short_link_failure_still_accepts()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _redirectResult = () => throw new InvalidOperationException(_fixture.Create<string>());

        // Act
        var result = await MoveToShow(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }
}
