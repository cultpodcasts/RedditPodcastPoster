using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Models;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class IdentityMetadataEnricherRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A film with only an IMDb id is looked up through TMDB find, then its TMDB movie metadata is read.")]
    public async Task film_with_only_imdb_id_uses_find()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var tmdbId = _fixture.CreateTmdbId();
        var metadata = new TmdbMovieMetadata(tmdbId, _fixture.CreateTitle(), null, null, null, null, null, []);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindMovieByImdbIdAsync(imdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbSearchHit(tmdbId, metadata.Title, TmdbTitleKind.Movie, null));
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetMovieMetadataAsync(tmdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        var sut = _mocker.CreateInstance<IdentityMetadataEnricher>();

        // Act
        var result = await sut.FetchFilmAsync(null, imdbId);

        // Assert
        result.Should().Be(metadata);
    }

    [Fact(DisplayName =
        "A failed TMDB call is null, not an exception, so a curator save never fails because TMDB is down.")]
    public async Task failed_lookup_is_null()
    {
        // Arrange
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetMovieMetadataAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TMDB request failed with status 429."));
        var sut = _mocker.CreateInstance<IdentityMetadataEnricher>();

        // Act
        var result = await sut.FetchFilmAsync(_fixture.CreateTmdbId(), null);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV episode with a TheTVDB id is found on TMDB by the parent show's TMDB series id and the TheTVDB season and " +
        "episode number.")]
    public async Task tv_episode_resolves_through_thetvdb_and_the_show()
    {
        // Arrange
        var tvdbId = _fixture.CreateTvdbId();
        var showTmdbId = _fixture.CreateTmdbId();
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { TvdbId = tvdbId };
        _mocker.GetMock<ITheTvdbClient>()
            .Setup(client => client.GetEpisodeAsync(tvdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TheTvdbEpisode(
                tvdbId, _fixture.CreateTvdbId(), episode.Title, 2, 5, null, null,
                TmdbSeriesId: _fixture.CreateTmdbId(), TmdbEpisodeId: null, CanonicalUrl: null, ImdbUrl: null));
        var metadata = new TmdbEpisodeMetadata(_fixture.CreateTmdbId(), episode.Title, null, 44, null, null);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvEpisodeMetadataAsync(showTmdbId, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        var sut = _mocker.CreateInstance<IdentityMetadataEnricher>();

        // Act
        var result = await sut.FetchTvShowEpisodeAsync(episode, showTmdbId);

        // Assert
        result.Should().Be(metadata);
    }

    [Fact(DisplayName =
        "A TV episode with only an IMDb id is found through TMDB find, which gives its series, season and episode number.")]
    public async Task tv_episode_with_only_imdb_id_uses_find()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { ImdbId = imdbId };
        var reference = new TmdbEpisodeReference(_fixture.CreateTmdbId(), 1, 3);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.FindTvEpisodeByImdbIdAsync(imdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        var metadata = new TmdbEpisodeMetadata(_fixture.CreateTmdbId(), null, _fixture.CreateTitle(), null, null, null);
        _mocker.GetMock<ITmdbClient>()
            .Setup(client => client.GetTvEpisodeMetadataAsync(reference.SeriesId, 1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        var sut = _mocker.CreateInstance<IdentityMetadataEnricher>();

        // Act
        var result = await sut.FetchTvShowEpisodeAsync(episode, showTmdbId: null);

        // Assert
        result.Should().Be(metadata);
    }
}
