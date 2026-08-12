using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Cloudflare.Models;

/// <summary>
/// Twitter/OG card aspect for shortener KV <c>imageAspect</c>.
/// Wire values match the Api Worker (<c>wide</c> / <c>square</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShareImageAspect
{
    [JsonStringEnumMemberName("wide")]
    Wide,

    [JsonStringEnumMemberName("square")]
    Square
}
