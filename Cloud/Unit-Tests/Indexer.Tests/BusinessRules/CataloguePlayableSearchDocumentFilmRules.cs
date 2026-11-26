using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class CataloguePlayableSearchDocumentFilmRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A film search document has no seriesName or podcastName, because a film has no parent, and both " +
        "are omitted from the JSON so only an Upload (replace) clears a stale uploader channel (#1428).")]
    public void film_document_clears_series_and_podcast_name()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle());
        film.SetRelease(CatalogueRelease.FromDateTimeUtc(DateTime.UtcNow));

        // Act
        var record = CataloguePlayableSearchDocument.For(film);

        // Assert
        record.ContentKind.Should().Be(SearchContentKind.Film);
        record.SeriesName.Should().BeNull();
        record.PodcastName.Should().BeNull();
        var json = JsonSerializer.Serialize(record);
        json.Should().NotContain("seriesName");
        json.Should().NotContain("podcastName");
    }
}
