using System.Linq.Expressions;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.News;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>
/// A news organisation / TV show PATCH keeps one submit target per service id: duplicates among outlets, shows and
/// channel-wide podcasts are 409s, and a playlist-scoped podcast may share the channel.
/// Also: a removal whose search clean-up fails is a 500, so the curator retries.
/// </summary>
public class ServiceIdsUniquenessRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<Podcast> _podcasts = [];
    private NewsOrganisation? _savedOrganisation;
    private TvShow? _savedShow;

    public ServiceIdsUniquenessRules()
    {
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>(),
                It.IsAny<Expression<Func<NewsOrganisation, Guid>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> match, Expression<Func<NewsOrganisation, Guid>> project) =>
                _organisations.Where(match.Compile()).Select(project.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>(),
                It.IsAny<Expression<Func<TvShow, Guid>>>()))
            .Returns((Expression<Func<TvShow, bool>> match, Expression<Func<TvShow, Guid>> project) =>
                _shows.Where(match.Compile()).Select(project.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> match) => _podcasts.Where(match.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(o => _savedOrganisation = o)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(s => _savedShow = s)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns(() => _organisations.Count == 0
                ? AsyncEnumerable.Empty<NewsReport>()
                : new[] { new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() } }.ToAsyncEnumerable());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns(AsyncEnumerable.Empty<TvShowEpisode>());
        _mocker.Use<IServiceIdsUniqueness>(new ServiceIdsUniqueness(
            _mocker.GetMock<INewsOrganisationRepository>().Object,
            _mocker.GetMock<ITvShowRepository>().Object,
            _mocker.GetMock<IPodcastRepository>().Object,
            NullLogger<ServiceIdsUniqueness>.Instance));
    }

    private NewsOrganisation AddOrganisation(CatalogueIndexingSource? indexing = null)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Indexing = indexing };
        _organisations.Add(organisation);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        return organisation;
    }

    private TvShow AddShow(CatalogueIndexingSource? indexing = null)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Indexing = indexing };
        _shows.Add(show);
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        return show;
    }

    private Podcast AddPodcast(Action<Podcast> customise)
    {
        var podcast = new Podcast { Id = _fixture.CreateGuid(), Name = _fixture.CreateTitle() };
        customise(podcast);
        _podcasts.Add(podcast);
        return podcast;
    }

    private Task<NewsOrganisationUpdateResult> Patch(NewsOrganisation organisation, NewsOrganisationChangeRequest change) =>
        _mocker.CreateInstance<NewsOrganisationService>()
            .UpdateAsync(new NewsOrganisationChangeRequestWrapper(organisation.Id, change), CancellationToken.None);

    private Task<TvShowUpdateResult> Patch(TvShow show, TvShowChangeRequest change) =>
        _mocker.CreateInstance<TvShowService>()
            .UpdateAsync(new TvShowChangeRequestWrapper(show.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH news organisation youTubeChannelId already on a TV show is a 409 naming the show and saves nothing, because submission could not tell the two apart.")]
    public async Task channel_on_another_parent_conflicts()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var show = AddShow(new CatalogueIndexingSource { YouTubeChannelId = channelId });
        var organisation = AddOrganisation();

        // Act
        var result = await Patch(organisation, new NewsOrganisationChangeRequest { YouTubeChannelId = $" {channelId} " });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Conflict);
        result.ConflictingIds.Should().Equal(show.Id);
        result.Message.Should().Contain("youTubeChannelId");
        _savedOrganisation.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation re-sending its own youTubeChannelId is accepted, because the parent being edited is not a duplicate of itself.")]
    public async Task own_channel_is_not_a_conflict()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(new CatalogueIndexingSource { YouTubeChannelId = channelId });

        // Act
        var result = await Patch(organisation, new NewsOrganisationChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _savedOrganisation!.Indexing!.YouTubeChannelId.Should().Be(channelId);
    }

    [Fact(DisplayName =
        "PATCH news organisation youTubeChannelId shared with a playlist-scoped podcast is accepted, because a show podcast with its own playlist on an outlet's channel is legitimate and submission routes by playlist membership.")]
    public async Task playlist_scoped_podcast_may_share_channel()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        AddPodcast(p => { p.YouTubeChannelId = channelId; p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId(); });
        var organisation = AddOrganisation();

        // Act
        var result = await Patch(organisation, new NewsOrganisationChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _savedOrganisation!.Indexing!.YouTubeChannelId.Should().Be(channelId);
    }

    [Fact(DisplayName =
        "PATCH TV show youTubeChannelId owned by a channel-wide podcast is a 409, because that podcast would win every submit from the channel.")]
    public async Task channel_wide_podcast_conflicts()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = AddPodcast(p => p.YouTubeChannelId = channelId);
        var show = AddShow();

        // Act
        var result = await Patch(show, new TvShowChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Conflict);
        result.ConflictingIds.Should().Equal(podcast.Id);
        _savedShow.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV show youTubeChannelId shared only with a removed channel-wide podcast is accepted, because submission ignores removed podcasts.")]
    public async Task removed_podcast_is_ignored()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        AddPodcast(p => { p.YouTubeChannelId = channelId; p.Removed = true; });
        var show = AddShow();

        // Act
        var result = await Patch(show, new TvShowChangeRequest { YouTubeChannelId = channelId });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
    }

    [Fact(DisplayName =
        "PATCH TV show spotifyId already on a podcast or another outlet is a 409, because a Spotify show id names one submit target.")]
    public async Task spotify_duplicate_conflicts()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var podcast = AddPodcast(p => p.SpotifyId = spotifyId);
        var organisation = AddOrganisation(new CatalogueIndexingSource { SpotifyId = spotifyId });
        var show = AddShow();

        // Act
        var result = await Patch(show, new TvShowChangeRequest { SpotifyId = spotifyId });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Conflict);
        result.ConflictingIds.Should().BeEquivalentTo([organisation.Id, podcast.Id]);
    }

    [Fact(DisplayName =
        "PATCH news organisation appleId already on another outlet is a 409, and nullAppleId is never checked, because clearing an id cannot create a duplicate.")]
    public async Task apple_duplicate_conflicts_and_clear_does_not()
    {
        // Arrange
        var appleId = _fixture.CreateAppleId();
        var other = AddOrganisation(new CatalogueIndexingSource { AppleId = appleId });
        var organisation = AddOrganisation();

        // Act
        var result = await Patch(organisation, new NewsOrganisationChangeRequest { AppleId = appleId });
        var cleared = await Patch(organisation, new NewsOrganisationChangeRequest { AppleId = appleId, NullAppleId = true });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Conflict);
        result.ConflictingIds.Should().Equal(other.Id);
        cleared.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
    }

    [Fact(DisplayName =
        "PATCH news organisation removed=true whose report search-document delete fails is Failed (500), because the curator must retry to finish the clean-up.")]
    public async Task failed_search_cleanup_is_failure()
    {
        // Arrange
        var organisation = AddOrganisation();
        _search.Client
            .Setup(c => c.DeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                SearchModelFactory.IndexDocumentsResult([SearchModelFactory.IndexingResult("id", "synthetic", false, 500)]),
                Mock.Of<Response>()));

        // Act
        var result = await Patch(organisation, new NewsOrganisationChangeRequest { Removed = true });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Failed);
        _savedOrganisation!.IsRemoved().Should().BeTrue();
    }
}
