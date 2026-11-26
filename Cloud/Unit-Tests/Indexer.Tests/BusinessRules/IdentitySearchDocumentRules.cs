using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>RPP #1430 / #1431: identity image and alternative titles on the film and TV episode search documents.</summary>
public class IdentitySearchDocumentRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A film's alternative titles go on its search document so site search finds it by any of them.")]
    public void film_document_carries_alternative_titles()
    {
        // Arrange
        var film = new Film("Faderhuset") { AlternativeTitles = ["Father's House", "La casa del padre"] };

        // Act
        var record = CataloguePlayableSearchDocument.For(film);

        // Assert
        record.AlternativeTitles.Should().Equal("Father's House", "La casa del padre");
    }

    [Fact(DisplayName =
        "A film with no alternative titles ever set omits the field, and a cleared list sends [] so the index drops the " +
        "old titles (merge ignores null).")]
    public void film_alternative_titles_omitted_or_cleared()
    {
        // Arrange
        var neverSet = new Film(_fixture.CreateTitle());
        var cleared = new Film(_fixture.CreateTitle()) { AlternativeTitles = [] };

        // Act
        var neverSetRecord = CataloguePlayableSearchDocument.For(neverSet);
        var clearedRecord = CataloguePlayableSearchDocument.For(cleared);

        // Assert
        neverSetRecord.AlternativeTitles.Should().BeNull();
        clearedRecord.AlternativeTitles.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName =
        "A film with no service image uses its identity image on the search document, so the film page has an image.")]
    public void film_without_service_image_uses_identity_image()
    {
        // Arrange
        var image = new Uri("https://image.tmdb.org/t/p/w1280/backdrop.jpg");
        var film = new Film(_fixture.CreateTitle()) { Image = image };

        // Act
        var record = CataloguePlayableSearchDocument.For(film);

        // Assert
        record.Image.Should().Be(image.ToString());
    }

    [Fact(DisplayName =
        "A streaming service image wins over the identity image on film and TV episode documents.")]
    public void service_image_wins_over_identity_image()
    {
        // Arrange
        var serviceImage = new Uri("https://example.org/service.jpg");
        var services = new Dictionary<string, ServiceLink>
        {
            ["youtube"] = new() { Url = new Uri("https://www.youtube.com/watch?v=abcdefghijk"), Image = serviceImage }
        };
        var film = new Film(_fixture.CreateTitle())
        {
            Image = new Uri("https://image.tmdb.org/t/p/w1280/backdrop.jpg"),
            Services = services
        };
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Image = new Uri("https://image.tmdb.org/t/p/w780/still.jpg"),
            Services = services
        };

        // Act
        var filmRecord = CataloguePlayableSearchDocument.For(film);
        var episodeRecord = CataloguePlayableSearchDocument.For(episode);

        // Assert
        filmRecord.Image.Should().Be(serviceImage.ToString());
        episodeRecord.Image.Should().Be(serviceImage.ToString());
    }

    [Fact(DisplayName =
        "Plain English rule: when a film has an identity image and a trailer thumbnail but no service image, " +
        "the search document uses the identity image, because trailer art must not replace the TMDB image.")]
    public void identity_image_wins_over_trailer_thumbnail()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var identityImage = _fixture.Create<Uri>();
        var film = new Film(_fixture.CreateTitle())
        {
            Image = identityImage,
            Trailers = [TrailerWithImage(_fixture.DefaultYouTubeImage(youTubeId), youTubeId)]
        };

        // Act
        var record = CataloguePlayableSearchDocument.For(film);

        // Assert
        record.Image.Should().Be(identityImage.ToString());
    }

    [Fact(DisplayName =
        "Plain English rule: when a film has a service image and a trailer thumbnail but no identity image, " +
        "the search document uses the service image, because a streaming image wins over a trailer thumbnail.")]
    public void service_image_wins_over_trailer_thumbnail_when_identity_image_is_absent()
    {
        // Arrange
        var serviceYouTubeId = _fixture.CreateYouTubeId();
        var trailerYouTubeId = _fixture.CreateYouTubeId();
        var serviceImage = _fixture.Create<Uri>();
        var film = new Film(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink>
            {
                ["youtube"] = new()
                {
                    Url = _fixture.DefaultYouTubeUrl(serviceYouTubeId),
                    Image = serviceImage
                }
            },
            Trailers = [TrailerWithImage(_fixture.Create<Uri>(), trailerYouTubeId)]
        };

        // Act
        var record = CataloguePlayableSearchDocument.For(film);

        // Assert
        record.Image.Should().Be(serviceImage.ToString());
    }

    [Fact(DisplayName =
        "Plain English rule: when a film has a service image, an identity image, and a trailer thumbnail, " +
        "the search document uses the service image, because the service image is chosen before identity or trailer art.")]
    public void service_image_wins_over_identity_image_and_trailer_thumbnail()
    {
        // Arrange
        var serviceYouTubeId = _fixture.CreateYouTubeId();
        var trailerYouTubeId = _fixture.CreateYouTubeId();
        var serviceImage = _fixture.Create<Uri>();
        var film = new Film(_fixture.CreateTitle())
        {
            Image = _fixture.Create<Uri>(),
            Services = new Dictionary<string, ServiceLink>
            {
                ["youtube"] = new()
                {
                    Url = _fixture.DefaultYouTubeUrl(serviceYouTubeId),
                    Image = serviceImage
                }
            },
            Trailers = [TrailerWithImage(_fixture.Create<Uri>(), trailerYouTubeId)]
        };

        // Act
        var record = CataloguePlayableSearchDocument.For(film);

        // Assert
        record.Image.Should().Be(serviceImage.ToString());
    }

    [Fact(DisplayName =
        "A TV episode with no service image uses its identity still on the push document and in the pull SQL.")]
    public void tv_episode_without_service_image_uses_still()
    {
        // Arrange
        var still = new Uri("https://image.tmdb.org/t/p/w780/still.jpg");
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Image = still };

        // Act
        var record = CataloguePlayableSearchDocument.For(episode);
        var sql = PlayableSearchSql.SiblingQuery(
            new PlayableSearchSql.SiblingProjection(
                SearchContentKind.TvShowEpisode, "c.title", "c.tvShowName", IncludeSeries: true),
            StreamingServiceCatalog.SearchEncodedKeys,
            StreamingServiceCatalog.ImageCoalesceOrder);

        // Assert
        record.Image.Should().Be(still.ToString());
        sql.Should().Contain("?? c.image) ?? \"\") as image");
    }

    [Fact(DisplayName =
        "A film's IMDb and TMDB ids go on its search document so the film page can render those buttons, and unset " +
        "ids are omitted (RPP #1437).")]
    public void film_document_carries_identity_ids()
    {
        // Arrange
        var imdbId = "tt" + _fixture.CreateAppleId();
        var tmdbId = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        if (tmdbId == 0) { tmdbId = 1; }
        var withIds = new Film(_fixture.CreateTitle()) { ImdbId = imdbId, TmdbId = tmdbId };
        var withoutIds = new Film(_fixture.CreateTitle());

        // Act
        var withIdsRecord = CataloguePlayableSearchDocument.For(withIds);
        var withoutIdsRecord = CataloguePlayableSearchDocument.For(withoutIds);

        // Assert
        withIdsRecord.ImdbId.Should().Be(imdbId);
        withIdsRecord.TmdbId.Should().Be(tmdbId);
        withoutIdsRecord.ImdbId.Should().BeNull();
        withoutIdsRecord.TmdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV episode's own and series IMDb, TMDB and TVDB ids go on its search document so episode and hub pages " +
        "can render those buttons, and unset ids are omitted (RPP #1437).")]
    public void tv_episode_document_carries_identity_and_series_ids()
    {
        // Arrange
        var imdbId = "tt" + _fixture.CreateAppleId();
        var seriesImdbId = "tt" + _fixture.CreateAppleId();
        var tmdbId = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        if (tmdbId == 0) { tmdbId = 1; }
        var seriesTmdbId = tmdbId + 1;
        var tvdbId = _fixture.CreateAppleId();
        var seriesTvdbId = tvdbId + 1;
        var withIds = new TvShowEpisode(_fixture.CreateTitle())
        {
            ImdbId = imdbId,
            TmdbId = tmdbId,
            TvdbId = tvdbId,
            SeriesImdbId = seriesImdbId,
            SeriesTmdbId = seriesTmdbId,
            SeriesTvdbId = seriesTvdbId
        };
        var withoutIds = new TvShowEpisode(_fixture.CreateTitle());

        // Act
        var withIdsRecord = CataloguePlayableSearchDocument.For(withIds);
        var withoutIdsRecord = CataloguePlayableSearchDocument.For(withoutIds);

        // Assert
        withIdsRecord.ImdbId.Should().Be(imdbId);
        withIdsRecord.TmdbId.Should().Be(tmdbId);
        withIdsRecord.TvdbId.Should().Be(tvdbId);
        withIdsRecord.SeriesImdbId.Should().Be(seriesImdbId);
        withIdsRecord.SeriesTmdbId.Should().Be(seriesTmdbId);
        withIdsRecord.SeriesTvdbId.Should().Be(seriesTvdbId);
        withoutIdsRecord.ImdbId.Should().BeNull();
        withoutIdsRecord.TmdbId.Should().BeNull();
        withoutIdsRecord.TvdbId.Should().BeNull();
        withoutIdsRecord.SeriesImdbId.Should().BeNull();
        withoutIdsRecord.SeriesTmdbId.Should().BeNull();
        withoutIdsRecord.SeriesTvdbId.Should().BeNull();
    }

    private FilmTrailer TrailerWithImage(Uri image, string youTubeId) =>
        new()
        {
            Id = _fixture.CreateGuid(),
            Title = _fixture.CreateTitle(),
            Url = _fixture.DefaultYouTubeUrl(youTubeId),
            Image = image
        };
}
