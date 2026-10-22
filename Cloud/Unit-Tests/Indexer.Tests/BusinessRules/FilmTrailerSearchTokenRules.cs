using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class FilmTrailerSearchTokenRules
{
    private readonly DomainTestFixture _fixture = new();

    private FilmTrailer CreateTrailer(TimeSpan length, string? title = null) => new()
    {
        Id = _fixture.CreateGuid(),
        Title = title ?? _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = length
    };

    [Fact(DisplayName =
        "A trailer token is duration|url|title with whole-second durations trimmed like the search duration field.")]
    public void token_is_duration_url_title()
    {
        // Arrange
        var trailer = CreateTrailer(TimeSpan.FromSeconds(131));

        // Act
        var token = FilmTrailerSearchToken.Format(trailer);

        // Assert
        token.Should().Be($"00:02:11|{trailer.Url.OriginalString}|{trailer.Title}");
    }

    [Fact(DisplayName =
        "A title containing the separator stays whole after the second separator, so titles need no escaping.")]
    public void title_with_separator_is_last()
    {
        // Arrange
        var title = $"{_fixture.CreateTitle()} | {_fixture.CreateTitle()}";
        var trailer = CreateTrailer(TimeSpan.FromMilliseconds(131_991), title);

        // Act
        var parts = FilmTrailerSearchToken.Format(trailer).Split(FilmTrailerSearchToken.Separator, 3);

        // Assert
        parts[0].Should().Be(trailer.Length.ToString());
        parts[1].Should().Be(trailer.Url.OriginalString);
        parts[2].Should().Be(title);
    }

    [Fact(DisplayName =
        "The film search document carries its trailer tokens in order, and omits the field when the film has none.")]
    public void film_document_carries_trailers()
    {
        // Arrange
        var first = CreateTrailer(_fixture.CreateDuration());
        var second = CreateTrailer(_fixture.CreateDuration());
        var withTrailers = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Trailers = [first, second] };
        var withoutTrailers = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };

        // Act
        var document = CataloguePlayableSearchDocument.For(withTrailers);
        var bare = CataloguePlayableSearchDocument.For(withoutTrailers);

        // Assert
        document.Trailers.Should().Equal(FilmTrailerSearchToken.Format(first), FilmTrailerSearchToken.Format(second));
        bare.Trailers.Should().BeNull();
    }

    [Fact(DisplayName =
        "Only the film pull query projects trailers, with the same duration trim as the push path, so TV and news documents never get the field.")]
    public void only_film_sql_projects_trailers()
    {
        // Arrange
        var keys = StreamingServiceCatalog.SearchEncodedKeys;
        var images = StreamingServiceCatalog.ImageCoalesceOrder;

        // Act
        var film = PlayableSearchSql.SiblingQuery(
            new PlayableSearchSql.SiblingProjection(
                SearchContentKind.Film, "c.name", null, IncludeSeries: false, IncludeTrailers: true),
            keys,
            images);
        var tv = PlayableSearchSql.SiblingQuery(
            new PlayableSearchSql.SiblingProjection(
                SearchContentKind.TvShowEpisode, "c.title", "c.tvShowName", IncludeSeries: true),
            keys,
            images);

        // Assert
        film.Should().Contain($"{FilmTrailerSearchToken.CosmosSql("c")} as trailers");
        film.Should().Contain("FROM t IN c.trailers");
        film.Should().Contain("\".0000000\"");
        tv.Should().NotContain("trailers");
    }
}
