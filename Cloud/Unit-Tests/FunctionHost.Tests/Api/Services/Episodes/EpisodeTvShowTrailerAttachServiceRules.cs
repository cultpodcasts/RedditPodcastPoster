using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.TvShows;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

/// <summary>
/// RPP #1412: Attach as TV show trailer. Mirrors <see cref="EpisodeTrailerAttachServiceRules"/> for films, with three
/// differences these rules pin: the attach never raises <c>latestReleased</c>, no show search document is pushed (a TV
/// show has none), and the short link points at the TV show page.
/// </summary>
public class EpisodeTvShowTrailerAttachServiceRules
{
    private const string ServiceKeysYouTube = RedditPodcastPoster.Models.Podcasts.ServiceKeys.YouTube;

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    // Stored as JSON with a version, like Cosmos: every read is a copy and a replace must match the ETag it read.
    private readonly Dictionary<Guid, (string Json, int Version)> _shows = [];
    private int _showSaves;
    private Action? _beforeReplace;
    private Action? _beforeVerifyRead;
    private int _showReads;
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private readonly List<Guid> _searchDeletes = [];
    private bool _searchUploadSucceeds = true;
    private bool _searchDeleteSucceeds = true;
    private readonly List<(Guid EpisodeId, string Path)> _redirects = [];
    private Func<WriteResult> _redirectResult = () => new WriteResult(true);
    private readonly List<TvShow> _createdShows = [];
    private int _createAttempts;
    // Holds: stop the first call at that step until the test releases it, so a second call runs in between.
    private Hold? _createHold;
    private Hold? _nameLookupHold;
    private readonly InMemoryTrailerClaims _claims = new();
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private Film? _filmWithEpisodeId;

    public EpisodeTvShowTrailerAttachServiceRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<ITvShowTrailerClaimStore>(_claims);
        _mocker.Use(TimeProvider.System);
        var shows = _mocker.GetMock<ITvShowRepository>();
        shows
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
            {
                if (++_showReads > 1)
                {
                    _beforeVerifyRead?.Invoke();
                }

                return Stored(id);
            });
        shows
            .Setup(r => r.GetTvShowForUpdate(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => Stored(id) is { } show
                ? new TvShowVersion(show, _shows[id].Version.ToString())
                : null);
        shows
            .Setup(r => r.GetTvShowIdsByTrailerId(It.IsAny<Guid>()))
            .ReturnsAsync((Guid trailerId) =>
                (IReadOnlyList<Guid>)_shows.Keys
                    .Select(Stored)
                    .Where(show => show?.HasTrailer(trailerId) == true)
                    .Select(show => show!.Id)
                    .ToArray());
        shows
            .Setup(r => r.TryReplace(It.IsAny<TvShow>(), It.IsAny<string>()))
            .ReturnsAsync((TvShow show, string etag) =>
            {
                var hook = _beforeReplace;
                _beforeReplace = null;
                hook?.Invoke();
                if (!_shows.TryGetValue(show.Id, out var stored) || stored.Version.ToString() != etag)
                {
                    return false;
                }

                _showSaves++;
                _shows[show.Id] = (JsonSerializer.Serialize(show), stored.Version + 1);
                return true;
            });
        shows
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector) => NameLookup(selector));
        shows
            .Setup(r => r.TryCreate(It.IsAny<TvShow>()))
            .Returns((TvShow show) => TryCreate(show));
        var tvEpisodes = _mocker.GetMock<ITvShowEpisodeRepository>();
        tvEpisodes
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> selector) =>
                _tvEpisodes.FirstOrDefault(selector.Compile()));
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _filmWithEpisodeId?.Id == id ? _filmWithEpisodeId : null);
        _mocker.GetMock<IFileKeyAllocator>()
            .Setup(a => a.Allocate(FileKeyKind.TvShow, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((FileKeyKind _, string name, Guid _, string? _) => $"allocated-{name.ToLowerInvariant()}");
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveTvShowAsync(
                It.IsAny<string>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
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
            .ReturnsAsync(() => _redirectResult());
        _mocker.GetMock<ILogger<EpisodeTvShowTrailerAttachService>>();
    }

    private TvShow SeedShow(Action<TvShow>? configure = null)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(show);
        _shows[show.Id] = (JsonSerializer.Serialize(show), 1);
        return show;
    }

    /// <summary>Cosmos create: false when a document already has the id. Waits at <see cref="_createHold"/> when set.</summary>
    private async Task<bool> TryCreate(TvShow show)
    {
        _createAttempts++;
        await Hold.At(_createHold);
        if (_shows.ContainsKey(show.Id))
        {
            return false;
        }

        _createdShows.Add(show);
        _shows[show.Id] = (JsonSerializer.Serialize(show), 1);
        return true;
    }

    /// <summary>The query result is taken on arrival (before any barrier), as a Cosmos query already in flight.</summary>
    private async IAsyncEnumerable<TvShow> NameLookup(Expression<Func<TvShow, bool>> selector)
    {
        var matches = _shows.Keys.Select(id => Stored(id)!).Where(selector.Compile()).ToArray();
        await Hold.At(_nameLookupHold);
        foreach (var show in matches)
        {
            yield return show;
        }
    }

    private TvShow? Stored(Guid id) =>
        _shows.TryGetValue(id, out var stored) ? JsonSerializer.Deserialize<TvShow>(stored.Json) : null;

    /// <summary>Another writer saves the show (unconditionally, as a plain upsert does).</summary>
    private void WriteConcurrently(Guid showId, Action<TvShow> change)
    {
        var show = Stored(showId)!;
        change(show);
        _shows[showId] = (JsonSerializer.Serialize(show), _shows[showId].Version + 1);
    }

    private TvShowTrailer CreateTrailer() => new()
    {
        Id = _fixture.CreateGuid(),
        Title = _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = _fixture.CreateDuration()
    };

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

    private Task<EpisodeTvShowTrailerAttachResult> Attach(
        Guid podcastId,
        Guid episodeId,
        Guid tvShowId,
        string? title = null) =>
        _mocker.CreateInstance<EpisodeTvShowTrailerAttachService>().AttachAsync(
            new EpisodeTvShowTrailerAttachCommand(
                podcastId,
                episodeId,
                new EpisodeTvShowTrailerAttachRequest { TvShowId = tvShowId, Title = title }),
            CancellationToken.None);

    private Task<EpisodeTvShowTrailerAttachResult> AttachCreating(
        Guid podcastId,
        Guid episodeId,
        string tvShowName,
        string? publisher = null) =>
        _mocker.CreateInstance<EpisodeTvShowTrailerAttachService>().AttachAsync(
            new EpisodeTvShowTrailerAttachCommand(
                podcastId,
                episodeId,
                new EpisodeTvShowTrailerAttachRequest
                {
                    CreateShow = true,
                    TvShowName = tvShowName,
                    Publisher = publisher
                }),
            CancellationToken.None);

    [Fact(DisplayName =
        "Attaching an episode adds a trailer with the episode id, title, YouTube URL and duration to the TV show, then deletes only that episode.")]
    public async Task attach_adds_trailer_and_deletes_episode()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        var sibling = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _episodes.Seed(sibling);

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        result.TvShowId.Should().Be(show.Id);
        var trailer = Stored(show.Id)!.Trailers.Should().ContainSingle().Subject;
        trailer.Id.Should().Be(episode.Id);
        trailer.Title.Should().Be(episode.Title.Trim());
        trailer.Url.Should().Be(EpisodeServicePresence.TryGetUrl(episode, ServiceKeysYouTube));
        trailer.Length.Should().Be(episode.Length);
        _showSaves.Should().Be(1);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, sibling.Id)).Should().NotBeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A title in the request names the trailer, so a curator can rename the clip as it is attached.")]
    public async Task attach_uses_request_title()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        var title = _fixture.CreateTitle();

        // Act
        await Attach(podcast.Id, episode.Id, show.Id, $"  {title}  ");

        // Assert
        Stored(show.Id)!.Trailers.Should().ContainSingle().Which.Title.Should().Be(title);
    }

    [Fact(DisplayName =
        "Attaching a trailer never raises the TV show's latestReleased, because a trailer is not a TV episode release.")]
    public async Task attach_never_raises_latest_released()
    {
        // Arrange
        var latest = DomainTestFixture.UtcDaysAgo(30);
        var show = SeedShow(s => s.LatestReleased = latest);
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        Stored(show.Id)!.LatestReleased.Should().Be(latest);
        _mocker.GetMock<ITvShowRepository>()
            .Verify(r => r.RaiseLatestReleased(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact(DisplayName =
        "Only the episode search document is deleted and nothing is uploaded, because a TV show has no search document to push.")]
    public async Task attach_only_deletes_episode_search_document()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        _searchDeletes.Should().Equal(episode.Id);
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "The episode short link is pointed at the TV show page, so old trailer links land on the show.")]
    public async Task attach_redirects_short_link_to_tv_show()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        _redirects.Should().ContainSingle()
            .Which.Should().Be((episode.Id, CatalogueShortId.TvShowPath(show.Name)));
    }

    [Fact(DisplayName =
        "A failed short-link redirect does not fail the attach, because the show and search are already correct.")]
    public async Task redirect_failure_still_accepts()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _redirectResult = () => throw new InvalidOperationException(_fixture.CreateTitle());

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "A failed episode search delete keeps the episode row and reports an indexing failure, so search never points at a deleted episode and a re-send can resume.")]
    public async Task search_delete_failure_keeps_episode()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _searchDeleteSucceeds = false;

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _redirects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Attaching an episode that is already a trailer of another TV show is a Conflict naming that show, the second show is left unchanged, and the episode row stays, because one episode cannot be a trailer of two shows.")]
    public async Task episode_already_a_trailer_of_another_show_is_rejected()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var existing = CreateTrailer();
        existing.Id = episode.Id;
        var holding = SeedShow(show => show.Trailers = [existing]);
        var target = SeedShow();

        // Act
        var result = await Attach(podcast.Id, episode.Id, target.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        result.TvShowId.Should().Be(holding.Id);
        result.Error.Should().Be("This episode is already a trailer of another TV show");
        Stored(target.Id)!.Trailers.Should().BeNull();
        Stored(holding.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _showSaves.Should().Be(0);
        _searchDeletes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the lookup of TV shows already holding the trailer id fails, the attach fails and the episode stays, so a trailer is not added while that check is unknown.")]
    public async Task trailer_lookup_failure_keeps_episode()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShowIdsByTrailerId(episode.Id))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeFalse();
        _showSaves.Should().Be(0);
        _searchDeletes.Should().BeEmpty();
        Stored(show.Id)!.Trailers.Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "The TV show being attached to is not treated as another holder of the trailer, so a lookup that already lists this show still adds the trailer.")]
    public async Task destination_show_in_the_trailer_lookup_does_not_block_the_attach()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShowIdsByTrailerId(episode.Id))
            .ReturnsAsync([show.Id]);

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        Stored(show.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-sending after an interrupted attach does not add the trailer twice and finishes deleting the episode.")]
    public async Task resend_after_interrupted_attach_resumes()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _searchDeleteSucceeds = false;
        await Attach(podcast.Id, episode.Id, show.Id);
        _searchDeleteSucceeds = true;
        _showReads = 0;

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        Stored(show.Id)!.Trailers.Should().ContainSingle();
        _showSaves.Should().Be(1);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "Re-sending after a completed attach is accepted for the same TV show, so a double click or lost 202 is harmless.")]
    public async Task resend_after_completed_attach_is_accepted()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        await Attach(podcast.Id, episode.Id, show.Id);

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        result.TvShowId.Should().Be(show.Id);
        Stored(show.Id)!.Trailers.Should().ContainSingle();
        _searchDeletes.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A missing TV show, episode or podcast is not found and nothing is written.")]
    public async Task missing_targets_are_not_found()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        var noShow = await Attach(podcast.Id, episode.Id, _fixture.CreateGuid());
        var noEpisode = await Attach(podcast.Id, _fixture.CreateGuid(), show.Id);
        var noPodcast = await Attach(_fixture.CreateGuid(), episode.Id, show.Id);

        // Assert
        noShow.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.NotFound);
        noEpisode.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.NotFound);
        noPodcast.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.NotFound);
        _showSaves.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A removed episode, a removed TV show, a missing show id, or an episode with no link is a bad request and nothing is written.")]
    public async Task invalid_targets_are_bad_requests()
    {
        // Arrange
        var show = SeedShow();
        var removedShow = SeedShow(s => s.Removed = true);
        var (podcast, episode) = SeedEpisode();
        var (removedPodcast, removedEpisode) = SeedEpisode(e => e.Removed = true);
        var (linklessPodcast, linkless) = SeedEpisode(e => e.Services = null);

        // Act
        var results = new[]
        {
            await Attach(removedPodcast.Id, removedEpisode.Id, show.Id),
            await Attach(podcast.Id, episode.Id, removedShow.Id),
            await Attach(podcast.Id, episode.Id, Guid.Empty),
            await Attach(linklessPodcast.Id, linkless.Id, show.Id)
        };

        // Assert
        results.Should().OnlyContain(r => r.Status == EpisodeTvShowTrailerAttachStatus.BadRequest);
        _showSaves.Should().Be(0);
        _searchDeletes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the episode id is already a TV episode or a film (an interrupted move), the attach is a Conflict and writes nothing, because deleting the episode search document would delete that item's document too.")]
    public async Task episode_id_already_a_tv_episode_or_film_is_conflict()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        var (filmPodcast, filmEpisode) = SeedEpisode();
        _tvEpisodes.Add(new TvShowEpisode(_fixture.CreateTitle()) { Id = episode.Id, TvShowId = show.Id });
        _filmWithEpisodeId = new Film(_fixture.CreateTitle()) { Id = filmEpisode.Id };

        // Act
        var tvEpisodeClash = await Attach(podcast.Id, episode.Id, show.Id);
        var filmClash = await Attach(filmPodcast.Id, filmEpisode.Id, show.Id);

        // Assert
        tvEpisodeClash.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        tvEpisodeClash.TvShowId.Should().Be(show.Id);
        filmClash.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        _showSaves.Should().Be(0);
        _searchDeletes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        (await _episodes.GetEpisode(filmPodcast.Id, filmEpisode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When another attach saves the TV show between this attach's read and save, both trailers are kept, because the save is ETag-guarded and re-reads on a conflict.")]
    public async Task concurrent_attach_keeps_both_trailers()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        var other = CreateTrailer();
        _beforeReplace = () => WriteConcurrently(show.Id, s => s.Trailers = [other]);

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        Stored(show.Id)!.Trailers!.Select(t => t.Id).Should().Equal(other.Id, episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When a stale TV show write drops the new trailer before the episode is deleted, the attach fails as an indexing failure and keeps the episode, and a re-send finishes it.")]
    public async Task dropped_trailer_keeps_episode_and_resend_finishes()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _beforeVerifyRead = () =>
        {
            _beforeVerifyRead = null;
            WriteConcurrently(show.Id, s => s.Trailers = null);
        };

        // Act
        var first = await Attach(podcast.Id, episode.Id, show.Id);
        var episodeAfterFirst = await _episodes.GetEpisode(podcast.Id, episode.Id);
        _showReads = 0;
        var resent = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        first.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        first.FailureIndexing.Should().BeTrue();
        episodeAfterFirst.Should().NotBeNull();
        resent.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        Stored(show.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When the TV show keeps changing on every save attempt, the attach fails without touching search or the episode.")]
    public async Task persistent_conflict_fails_and_keeps_episode()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.TryReplace(It.IsAny<TvShow>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        _searchDeletes.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        Stored(show.Id)!.Trailers.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the TV show is deleted after the trailer is saved, the episode search document is put back and the episode row is kept, and a re-send reports the show as not found.")]
    public async Task show_deleted_after_save_restores_episode_search_document()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _beforeVerifyRead = () =>
        {
            _beforeVerifyRead = null;
            _shows.Remove(show.Id);
        };

        // Act
        var first = await Attach(podcast.Id, episode.Id, show.Id);
        var episodeAfterFirst = await _episodes.GetEpisode(podcast.Id, episode.Id);
        var resent = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        first.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        first.FailureIndexing.Should().BeTrue();
        episodeAfterFirst.Should().NotBeNull();
        resent.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.NotFound);
        resent.Error.Should().Be("TV show not found");
        _searchDeletes.Should().Equal(episode.Id);
        _redirects.Should().BeEmpty();
        _uploads.Should().ContainSingle().Which.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord());
        LoggedShowGone(LogLevel.Warning, episode.Id, show.Id);
    }

    [Fact(DisplayName =
        "When the TV show is deleted after the trailer is saved and putting the episode search document back fails, the episode row is still kept.")]
    public async Task show_deleted_after_save_keeps_episode_when_search_restore_fails()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        _beforeVerifyRead = () =>
        {
            _beforeVerifyRead = null;
            _shows.Remove(show.Id);
            _searchUploadSucceeds = false;
        };

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _uploads.Should().ContainSingle().Which.Should().ContainSingle().Which.Id.Should().Be(episode.Id.ToString());
        LoggedShowGone(LogLevel.Warning, episode.Id, show.Id);
        LoggedShowGone(LogLevel.Error, episode.Id, show.Id);
    }

    [Fact(DisplayName =
        "createShow with a name no TV show has creates a show with the given publisher, no indexing and an allocated file key, attaches the trailer to it and deletes the episode.")]
    public async Task create_show_creates_show_and_attaches_trailer()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var showName = _fixture.CreateTitle();
        var publisher = _fixture.CreateTitle();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, $"  {showName}  ", publisher);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        result.ShowCreated.Should().BeTrue();
        var created = _createdShows.Should().ContainSingle().Subject;
        result.TvShowId.Should().Be(created.Id);
        result.Name.Should().Be(showName);
        var stored = Stored(created.Id)!;
        stored.Name.Should().Be(showName);
        stored.PublisherName.Should().Be(publisher);
        stored.Indexing.Should().BeNull();
        stored.FileKey.Should().Be($"allocated-{showName.ToLowerInvariant()}");
        stored.Id.Should().Be(EpisodeTvShowTrailerAttachService.NewTvShowId(showName));
        stored.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "createShow with a name one TV show already has (any case) attaches the trailer to that show and creates nothing, like Move to TV show joins an existing show.")]
    public async Task create_show_reuses_show_with_same_name()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, show.Name.ToUpperInvariant());

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        result.TvShowId.Should().Be(show.Id);
        result.ShowCreated.Should().BeFalse();
        _createdShows.Should().BeEmpty();
        Stored(show.Id)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "createShow when two TV shows already have the name returns 409 and writes nothing.")]
    public async Task create_show_with_two_shows_of_that_name_is_conflict()
    {
        // Arrange
        var first = SeedShow();
        SeedShow(show => show.Name = first.Name);
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, first.Name);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        _createdShows.Should().BeEmpty();
        _showSaves.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "createShow for a removed episode or an episode with no playable link returns 400 before any TV show is created.")]
    public async Task create_show_invalid_episode_creates_nothing()
    {
        // Arrange
        var (removedPodcast, removed) = SeedEpisode(e => e.Removed = true);
        var (linklessPodcast, linkless) = SeedEpisode(e => e.Services = null);

        // Act
        var removedResult = await AttachCreating(removedPodcast.Id, removed.Id, _fixture.CreateTitle());
        var linklessResult = await AttachCreating(linklessPodcast.Id, linkless.Id, _fixture.CreateTitle());

        // Assert
        removedResult.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.BadRequest);
        linklessResult.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.BadRequest);
        _createdShows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "createShow re-sent after a failed search delete reuses the show created by the first call, so the show is never created twice.")]
    public async Task create_show_resend_after_failure_reuses_created_show()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var showName = _fixture.CreateTitle();
        _searchDeleteSucceeds = false;
        var first = await AttachCreating(podcast.Id, episode.Id, showName);
        _searchDeleteSucceeds = true;

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, showName);

        // Assert
        first.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        first.TvShowId.Should().Be(result.TvShowId);
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        result.ShowCreated.Should().BeFalse();
        _createdShows.Should().ContainSingle().Which.Id.Should().Be(result.TvShowId!.Value);
        Stored(result.TvShowId!.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "The new TV show is written once, by a conditional create that already holds the trailer, so no empty show is ever stored.")]
    public async Task create_show_single_conditional_write_holds_trailer()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        _createdShows.Should().ContainSingle().Which.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _createAttempts.Should().Be(1);
        _showSaves.Should().Be(0);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(It.IsAny<TvShow>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the search delete fails after the create, the new TV show still holds the trailer and the episode row is kept, so nothing is orphaned and a re-send resumes.")]
    public async Task create_show_failure_after_create_leaves_show_with_trailer()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        _searchDeleteSucceeds = false;

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        Stored(result.TvShowId!.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Two overlapping createShow calls for the same name that both reach the create converge on one TV show with one trailer, and both are accepted.")]
    public async Task create_show_overlapping_creates_converge_on_one_show()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var showName = _fixture.CreateTitle();
        _createHold = new Hold();

        // Act
        var results = await Interleave(
            _createHold,
            () => AttachCreating(podcast.Id, episode.Id, showName),
            () => AttachCreating(podcast.Id, episode.Id, showName.ToUpperInvariant()));

        // Assert
        _createAttempts.Should().Be(2);
        results.Should().OnlyContain(r => r.Status == EpisodeTvShowTrailerAttachStatus.Accepted);
        results.Select(r => r.TvShowId).Distinct().Should().ContainSingle();
        results.Count(r => r.ShowCreated).Should().Be(1);
        var show = _shows.Keys.Select(Stored).Should().ContainSingle().Subject!;
        show.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When another call creates the TV show between this call's name query and its create, this call attaches to that show instead of returning 409.")]
    public async Task create_show_overlapping_name_checks_converge_on_one_show()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var showName = _fixture.CreateTitle();
        _nameLookupHold = new Hold();

        // Act
        var results = await Interleave(
            _nameLookupHold,
            () => AttachCreating(podcast.Id, episode.Id, showName),
            () => AttachCreating(podcast.Id, episode.Id, showName));

        // Assert
        results.Should().OnlyContain(r => r.Status == EpisodeTvShowTrailerAttachStatus.Accepted);
        results.Select(r => r.TvShowId).Distinct().Should().ContainSingle();
        _createdShows.Should().ContainSingle();
        _shows.Keys.Select(Stored).Should().ContainSingle()
            .Which!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "Two overlapping createShow calls with different names for one episode are decided by the one conditional claim: exactly one show is created and keeps the trailer, and the other call gets 409 naming it and writes nothing.")]
    public async Task create_show_different_names_overlapping_one_owner()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        _claims.Hold = new Hold();

        // Act
        var results = await Interleave(
            _claims.Hold,
            () => AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle()),
            () => AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle()));

        // Assert
        var winner = results.Should().ContainSingle(r => r.Status == EpisodeTvShowTrailerAttachStatus.Accepted).Subject;
        var loser = results.Should().ContainSingle(r => r.Status == EpisodeTvShowTrailerAttachStatus.Conflict).Subject;
        loser.TvShowId.Should().Be(winner.TvShowId);
        _createdShows.Should().ContainSingle().Which.Id.Should().Be(winner.TvShowId!.Value);
        _shows.Keys.Should().Equal(winner.TvShowId.Value);
        Stored(winner.TvShowId.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
        _claims.Owner(episode.Id).Should().Be(winner.TvShowId.Value);
    }

    [Fact(DisplayName =
        "While another attach's fresh claim holds the episode (its show not written yet), createShow returns 409 'in progress' naming that show and writes nothing.")]
    public async Task create_show_fresh_foreign_claim_is_conflict_without_writes()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var otherShowId = _fixture.CreateGuid();
        _claims.Seed(episode.Id, otherShowId, DateTime.UtcNow);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        result.TvShowId.Should().Be(otherShowId);
        result.Error.Should().Contain("in progress");
        _createAttempts.Should().Be(0);
        _shows.Should().BeEmpty();
        _claims.Owner(episode.Id).Should().Be(otherShowId);
        _mocker.GetMock<IFileKeyAllocator>().Verify(
            a => a.Allocate(It.IsAny<FileKeyKind>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A claim older than the stale window whose show never took the trailer (a failed attach) is taken over, and the attach completes.")]
    public async Task create_show_stale_claim_is_taken_over()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        _claims.Seed(
            episode.Id,
            _fixture.CreateGuid(),
            DateTime.UtcNow - EpisodeTvShowTrailerAttachService.ClaimStaleAfter - TimeSpan.FromMinutes(1));

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        _claims.Owner(episode.Id).Should().Be(result.TvShowId!.Value);
        Stored(result.TvShowId.Value)!.Trailers.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "Attaching by tvShowId while another show's fresh claim holds the episode returns 409 naming that show and leaves the target show unchanged.")]
    public async Task attach_by_id_foreign_claim_is_conflict_without_writes()
    {
        // Arrange
        var show = SeedShow();
        var (podcast, episode) = SeedEpisode();
        var otherShowId = _fixture.CreateGuid();
        _claims.Seed(episode.Id, otherShowId, DateTime.UtcNow);
        var before = _shows[show.Id];

        // Act
        var result = await Attach(podcast.Id, episode.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        result.TvShowId.Should().Be(otherShowId);
        _shows[show.Id].Should().Be(before);
        _showSaves.Should().Be(0);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the name's TV show id belongs to a show renamed since, createShow returns 409 naming it and does not attach to it.")]
    public async Task create_show_id_held_by_renamed_show_is_conflict()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var showName = _fixture.CreateTitle();
        var renamed = SeedShow(show => show.Id = EpisodeTvShowTrailerAttachService.NewTvShowId(showName));

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, showName);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        result.TvShowId.Should().Be(renamed.Id);
        Stored(renamed.Id)!.Trailers.Should().BeNullOrEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "The TV show id for a name ignores case and surrounding spaces, differs between names, and differs from a film id of the same name.")]
    public void new_tv_show_id_is_derived_from_normalised_name()
    {
        // Arrange
        var name = _fixture.CreateTitle();

        // Act
        var id = EpisodeTvShowTrailerAttachService.NewTvShowId(name);

        // Assert
        EpisodeTvShowTrailerAttachService.NewTvShowId($"  {name.ToUpperInvariant()} ").Should().Be(id);
        EpisodeTvShowTrailerAttachService.NewTvShowId(name + " 2").Should().NotBe(id);
        EpisodeTrailerAttachService.NewFilmId(name).Should().NotBe(id);
        id.ToString()[14].Should().Be('5');
    }

    [Fact(DisplayName =
        "createShow re-sent after a completed attach (episode row gone) is accepted for the TV show holding the trailer.")]
    public async Task create_show_resend_after_completed_attach_is_accepted()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var showName = _fixture.CreateTitle();
        var first = await AttachCreating(podcast.Id, episode.Id, showName);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, showName);

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Accepted);
        result.TvShowId.Should().Be(first.TvShowId);
        _createdShows.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "createShow for an episode that is already a trailer of a TV show with another name returns 409 naming that show.")]
    public async Task create_show_episode_held_by_other_show_is_conflict()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var holder = SeedShow(show => show.Trailers = [TvShowTrailerFactory.From(episode, null)!]);

        // Act
        var result = await AttachCreating(podcast.Id, episode.Id, _fixture.CreateTitle());

        // Assert
        result.Status.Should().Be(EpisodeTvShowTrailerAttachStatus.Conflict);
        result.TvShowId.Should().Be(holder.Id);
        _createdShows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "The request needs exactly one of tvShowId or tvShowName, a publisher only with createShow, and no '/' in the name, otherwise it is a 400.")]
    public async Task request_validation()
    {
        // Arrange
        var (podcast, episode) = SeedEpisode();
        var sut = _mocker.CreateInstance<EpisodeTvShowTrailerAttachService>();

        Task<EpisodeTvShowTrailerAttachResult> Send(EpisodeTvShowTrailerAttachRequest request) =>
            sut.AttachAsync(
                new EpisodeTvShowTrailerAttachCommand(podcast.Id, episode.Id, request),
                CancellationToken.None);

        // Act
        var results = new[]
        {
            await Send(new EpisodeTvShowTrailerAttachRequest { CreateShow = true, TvShowName = "  " }),
            await Send(new EpisodeTvShowTrailerAttachRequest
            {
                TvShowId = _fixture.CreateGuid(), CreateShow = true, TvShowName = _fixture.CreateTitle()
            }),
            await Send(new EpisodeTvShowTrailerAttachRequest
            {
                TvShowId = _fixture.CreateGuid(), Publisher = _fixture.CreateTitle()
            }),
            await Send(new EpisodeTvShowTrailerAttachRequest { CreateShow = true, TvShowName = "a/b" })
        };

        // Assert
        results.Should().OnlyContain(r => r.Status == EpisodeTvShowTrailerAttachStatus.BadRequest);
        _createdShows.Should().BeEmpty();
    }

    private void LoggedShowGone(LogLevel level, Guid episodeId, Guid tvShowId)
    {
        var fragments = new[] { "TvShowTrailerAttachShowGone", episodeId.ToString(), tvShowId.ToString() };
        _mocker.GetMock<ILogger<EpisodeTvShowTrailerAttachService>>().Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => fragments.All(fragment => state!.ToString()!.Contains(fragment))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Runs two calls in a set order, never at the same time: <paramref name="first"/> starts and stops at
    /// <paramref name="hold"/>, <paramref name="second"/> runs to completion, then <paramref name="first"/> is released
    /// and finishes. Every fake here completes synchronously, so <paramref name="first"/> has reached the hold by the
    /// time it returns its task; if it has not, the test fails here instead of waiting.
    /// </summary>
    private static async Task<EpisodeTvShowTrailerAttachResult[]> Interleave(
        Hold hold,
        Func<Task<EpisodeTvShowTrailerAttachResult>> first,
        Func<Task<EpisodeTvShowTrailerAttachResult>> second)
    {
        var held = first();
        hold.Reached.Should().BeTrue("the first call must stop at the hold before the second call starts");
        var secondResult = await second();
        hold.Release();
        return [await held, secondResult];
    }

    /// <summary>
    /// Stops the first call that reaches a step until the test releases it. Later calls go straight through, so the
    /// order of the two calls is set by the test, not by thread timing.
    /// </summary>
    private sealed class Hold
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public bool Reached => _arrivals > 0;

        public static Task At(Hold? hold) => hold?.Arrive() ?? Task.CompletedTask;

        public void Release() => _released.SetResult();

        private Task Arrive() => ++_arrivals == 1 ? _released.Task : Task.CompletedTask;
    }

    /// <summary>
    /// LookUps claims like Cosmos: a create fails when the id exists, a takeover must match the version it read.
    /// <see cref="Hold"/> stops the first claimer before it reads the claim, after it has passed every check.
    /// </summary>
    private sealed class InMemoryTrailerClaims : ITvShowTrailerClaimStore
    {
        private readonly Dictionary<Guid, (TvShowTrailerClaim Claim, int Version)> _claims = [];

        public Hold? Hold { get; set; }

        public void Seed(Guid episodeId, Guid tvShowId, DateTime claimedUtc) =>
            _claims[episodeId] = (
                new TvShowTrailerClaim { EpisodeId = episodeId, TvShowId = tvShowId, ClaimedUtc = claimedUtc }, 1);

        public Guid? Owner(Guid episodeId) => _claims.TryGetValue(episodeId, out var held) ? held.Claim.TvShowId : null;

        public async Task<TvShowTrailerClaimAttempt> TryClaim(Guid episodeId, Guid tvShowId, DateTime nowUtc)
        {
            await Hold.At(Hold);
            if (!_claims.TryGetValue(episodeId, out var held))
            {
                Seed(episodeId, tvShowId, nowUtc);
                return new TvShowTrailerClaimAttempt(true);
            }

            return held.Claim.TvShowId == tvShowId
                ? new TvShowTrailerClaimAttempt(true)
                : new TvShowTrailerClaimAttempt(false, held.Claim, held.Version.ToString());
        }

        public Task<bool> TryTakeOver(Guid episodeId, Guid tvShowId, DateTime nowUtc, string holderETag)
        {
            if (!_claims.TryGetValue(episodeId, out var held) || held.Version.ToString() != holderETag)
            {
                return Task.FromResult(false);
            }

            _claims[episodeId] = (
                new TvShowTrailerClaim { EpisodeId = episodeId, TvShowId = tvShowId, ClaimedUtc = nowUtc },
                held.Version + 1);
            return Task.FromResult(true);
        }
    }
}
