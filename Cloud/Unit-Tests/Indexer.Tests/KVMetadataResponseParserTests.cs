using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Cloudflare.Clients;
using RedditPodcastPoster.Cloudflare.Models;
using Xunit;

namespace Indexer.Tests;

public class KVMetadataResponseParserTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact(DisplayName =
        "KV metadata GET returns Cloudflare {success,result} envelope: parse into KVRecord with request key and metadata, because the API does not echo key/value.")]
    public void Parse_cloudflare_metadata_envelope_into_kv_record()
    {
        // Arrange
        const string key = "episode-key";
        var json =
            """{"success":true,"errors":[],"messages":[],"result":{"episodeTitle":"Sample title","releaseDate":"2026-07-29","duration":"00:30:00","image":"yhq"}}""";

        // Act
        var record = KVMetadataResponseParser.Parse(HttpStatusCode.OK, json, key, JsonOptions);

        // Assert
        record.Should().NotBeNull();
        record!.Key.Should().Be(key);
        record.Metadata.Should().NotBeNull();
        record.Metadata!.EpisodeTitle.Should().Be("Sample title");
        record.Metadata.Image.Should().Be("yhq");
    }

    [Fact(DisplayName =
        "KV metadata GET 404 means the shortener key is absent: return null so Write can create the record.")]
    public void Parse_not_found_as_null()
    {
        // Arrange
        var json = """{"success":false,"errors":[{"code":10009,"message":"Not found"}],"messages":[],"result":null}""";

        // Act
        var record = KVMetadataResponseParser.Parse(HttpStatusCode.NotFound, json, "missing-key", JsonOptions);

        // Assert
        record.Should().BeNull();
    }

    [Fact(DisplayName =
        "KV metadata GET with empty result object still means the key exists: return a KVRecord without throwing on missing MetaData fields.")]
    public void Parse_empty_result_as_existing_key_without_metadata()
    {
        // Arrange
        const string key = "episode-key";
        var json = """{"success":true,"errors":[],"messages":[],"result":{}}""";

        // Act
        var record = KVMetadataResponseParser.Parse(HttpStatusCode.OK, json, key, JsonOptions);

        // Assert
        record.Should().NotBeNull();
        record!.Key.Should().Be(key);
        record.Metadata.Should().BeNull();
    }
}
