using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class Episode
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; } = ModelType.Episode;

    [JsonPropertyName("title")]
    [JsonPropertyOrder(3)]
    public string Title { get; init; } = "";

    [JsonPropertyName("description")]
    [JsonPropertyOrder(4)]
    public string Description { get; set; } = "";

    [JsonPropertyName("posted")]
    [JsonPropertyOrder(5)]
    public bool Posted { get; set; } = false;

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(6)]
    public bool Ignored { get; set; } = false;

    [JsonPropertyName("release")]
    [JsonPropertyOrder(7)]
    public DateTime Release { get; init; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(8)]
    public TimeSpan Length { get; init; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(9)]
    public bool Explicit { get; init; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(10)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(11)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(12)]
    public string YouTubeId { get; set; } = "";

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(13)]

    public ServiceUrls Urls { get; set; } = new();

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(14)]
    public List<string> Subjects { get; set; } = new();

    public static Episode FromSpotify(string spotifyId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri spotifyUrl)
    {
        return new Episode
        {
            SpotifyId = spotifyId,
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release,
            Urls = new ServiceUrls
            {
                Spotify = spotifyUrl
            }
        };
    }

    public static Episode FromYouTube(
        string youTubeId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri youTubeUrl)
    {
        return new Episode
        {
            YouTubeId = youTubeId,
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release,
            Urls = new ServiceUrls {YouTube = youTubeUrl}
        };
    }
}