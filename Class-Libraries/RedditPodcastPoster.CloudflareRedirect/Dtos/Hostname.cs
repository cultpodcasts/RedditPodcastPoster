using System.Text.Json.Serialization;

namespace RedditPodcastPoster.CloudflareRedirect.Dtos;

public class Hostname
{
    [JsonPropertyName("url_hostname")]
    public string UrlHostname { get; set; }
}