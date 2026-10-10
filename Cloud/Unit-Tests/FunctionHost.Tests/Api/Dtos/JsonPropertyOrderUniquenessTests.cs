using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class JsonPropertyOrderUniquenessTests
{
    public static TheoryData<Type> TypesWithExplicitOrder =>
    [
        typeof(EpisodeImages), typeof(EpisodeImagesDto),
        typeof(ServiceUrls), typeof(ServiceUrlsDto),
        typeof(EpisodeDto), typeof(DiscoveryResponse.Item)
    ];

    [Theory(DisplayName =
        "When a type declares JsonPropertyOrder, each property has a unique order, so the JSON order is explicit rather than left to declaration order.")]
    [MemberData(nameof(TypesWithExplicitOrder))]
    public void json_property_orders_are_unique(Type type)
    {
        // Arrange
        var orders = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<JsonPropertyOrderAttribute>())
            .Where(a => a != null)
            .Select(a => a!.Order);

        // Act
        var duplicates = orders.GroupBy(o => o).Where(g => g.Count() > 1).Select(g => g.Key);

        // Assert
        duplicates.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When episode images and service urls are serialised, keys keep their previous order, so renumbering JsonPropertyOrder did not change the wire format.")]
    public void renumbered_types_keep_previous_key_order()
    {
        // Arrange
        var uri = new Uri("https://example.com/");
        var images = new EpisodeImages { YouTube = uri, Spotify = uri, Apple = uri, Other = uri };
        var urls = new ServiceUrls { Spotify = uri, Apple = uri, YouTube = uri, InternetArchive = uri, BBC = uri };

        // Act
        var imageKeys = Keys(JsonSerializer.Serialize(images));
        var urlKeys = Keys(JsonSerializer.Serialize(urls));

        // Assert
        imageKeys.Should().Equal("youtube", "spotify", "apple", "other");
        urlKeys.Should().Equal("spotify", "apple", "youtube", "internetArchive", "bbc");
    }

    private static string[] Keys(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).ToArray();
}
