using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class Episode
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(20)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; } = ModelType.Episode;

    [JsonPropertyName("title")]
    [JsonPropertyOrder(30)]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonPropertyOrder(40)]
    public string Description { get; set; } = "";

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(45)]
    public string? Language { get; set; }

    [JsonPropertyName("posted")]
    [JsonPropertyOrder(50)]
    public bool Posted { get; set; } = false;

    [JsonPropertyName("tweeted")]
    [JsonPropertyOrder(51)]
    public bool Tweeted { get; set; }

    [JsonPropertyName("bluesky")]
    [JsonPropertyOrder(52)]
    public bool? BlueskyPosted { get; set; }

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(60)]
    public bool Ignored { get; set; } = false;

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(61)]
    public bool Removed { get; set; } = false;

    [JsonPropertyName("release")]
    [JsonPropertyOrder(70)]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(71)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(72)]
    public bool Explicit { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(80)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(81)]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(82)]
    public string YouTubeId { get; set; } = "";

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(100)]
    public ServiceUrls Urls { get; set; } = new();

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(90)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(100)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("images")]
    [JsonPropertyOrder(150)]
    public EpisodeImages? Images { get; set; }

    [JsonPropertyName("twitterHandles")]
    [JsonPropertyOrder(160)]
    public string[]? TwitterHandles { get; set; }

    [JsonPropertyName("blueskyHandles")]
    [JsonPropertyOrder(160)]
    public string[]? BlueskyHandles { get; set; }

    public static Episode FromSpotify(string spotifyId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri spotifyUrl,
        Uri? maxImage)
    {
        var episode = new Episode
        {
            SpotifyId = spotifyId,
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release,
            Urls = new ServiceUrls {Spotify = spotifyUrl}
        };
        if (maxImage != null)
        {
            episode.Images = new EpisodeImages
            {
                Spotify = maxImage
            };
        }

        return episode;
    }

    public static Episode FromYouTube(
        string youTubeId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri youTubeUrl,
        Uri? image)
    {
        var episode = new Episode
        {
            YouTubeId = youTubeId,
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release,
            Urls = new ServiceUrls {YouTube = youTubeUrl}
        };
        if (image != null)
        {
            episode.Images = new EpisodeImages
            {
                YouTube = image
            };
        }

        return episode;
    }

    public static Episode FromApple(
        long appleId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri url,
        Uri? image)
    {
        var episode = new Episode
        {
            AppleId = appleId,
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release,
            Urls = new ServiceUrls {Apple = url}
        };
        if (image != null)
        {
            episode.Images = new EpisodeImages
            {
                Apple = image
            };
        }

        return episode;
    }
}