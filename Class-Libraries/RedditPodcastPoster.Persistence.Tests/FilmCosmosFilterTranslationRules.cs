using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

public class FilmCosmosFilterTranslationRules
{
    private readonly DomainTestFixture _fixture = new();

    private CosmosClient CreateClient() =>
        new(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(_fixture.CreateGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
            });

    [Fact(DisplayName =
        "When films are filtered by HoldsServiceOrTrailerUrl, then the Cosmos SQL matches the service link or any trailers[].url, because URL membership and the indexer must recognise a trailer URL as already in the catalogue.")]
    public void holds_service_or_trailer_url_translates_to_services_or_trailer_exists()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var pasted = new Uri($"https://youtu.be/{videoId}");
        var stored = new Uri($"https://www.youtube.com/watch?v={videoId}");
        using var client = CreateClient();
        var container = client.GetContainer("db", "films");

        // Act
        var sql = container.GetItemLinqQueryable<Film>()
            .Where(FilmCosmosFilters.HoldsServiceOrTrailerUrl(ServiceKeys.YouTube, pasted, stored))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"services\"]");
        sql.Should().Contain("EXISTS");
        sql.Should().Contain("[\"trailers\"]");
        sql.Should().Contain("[\"url\"]");
        sql.Should().Contain(stored.ToString());
        sql.Should().Contain(pasted.ToString());
    }
}
