using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Converters;

namespace RedditPodcastPoster.PodcastServices.YouTube.Configuration;

public class Application
{
    public required string ApiKey { get; set; }
    public required string Name { get; set; }

    [JsonConverter(typeof(ItemConverterDecorator<JsonStringEnumConverter>))]
    public ApplicationUsage Usage { get; set; }

    public required string DisplayName { get; set; }
}