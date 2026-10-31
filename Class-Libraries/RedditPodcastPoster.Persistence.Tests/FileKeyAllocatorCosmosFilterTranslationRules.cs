using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

public class FileKeyAllocatorCosmosFilterTranslationRules
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

    public static TheoryData<string> PublicKinds => [nameof(Podcast), nameof(TvShow), nameof(NewsOrganisation), nameof(Film)];

    [Theory(DisplayName =
        "When the file-key allocator reads taken keys for a public kind, then the Cosmos SQL filters on fileKey with STARTSWITH and an id exclusion, because a renamed member or a changed filter shape would match no stored key and hand out duplicate file names.")]
    [MemberData(nameof(PublicKinds))]
    public void taken_key_selector_translates_to_file_key(string kind)
    {
        // Arrange
        var candidate = _fixture.CreateTitle().ToLowerInvariant().Replace(' ', '_');
        var id = _fixture.CreateGuid();
        using var client = CreateClient();

        // Act
        var sql = kind switch
        {
            nameof(Podcast) => Translate<Podcast>(client, candidate, id),
            nameof(TvShow) => Translate<TvShow>(client, candidate, id),
            nameof(NewsOrganisation) => Translate<NewsOrganisation>(client, candidate, id),
            _ => Translate<Film>(client, candidate, id)
        };

        // Assert
        sql.Should().Contain("[\"fileKey\"]");
        sql.Should().Contain("STARTSWITH");
        sql.Should().Contain("[\"id\"]");
        sql.Should().NotContain("FileKey");
    }

    [Fact(DisplayName =
        "When Publisher declares FileKey, then it carries JsonPropertyName fileKey, because the stored and queried name must not depend on the serializer's camelCase naming policy.")]
    public void publisher_file_key_pins_json_name()
    {
        // Arrange
        var property = typeof(Publisher).GetProperty(nameof(Publisher.FileKey))!;

        // Act
        var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Name.Should().Be("fileKey");
    }

    private static string Translate<TEntity>(CosmosClient client, string candidate, Guid id) where TEntity : Publisher
    {
        Expression<Func<TEntity, bool>> selector = FileKeyAllocator.TakenKeySelector<TEntity>(candidate, id);
        return client.GetContainer("db", "catalogue")
            .GetItemLinqQueryable<TEntity>()
            .Where(selector)
            .Select(x => x.FileKey)
            .ToQueryDefinition()
            .QueryText;
    }
}
