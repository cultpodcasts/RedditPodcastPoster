using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Canonical streaming catalogue destinations (not Spotify/Apple/YouTube).
/// Declaration order is search-encode / contract <c>streamingServiceKeys</c> order.
/// Wire keys are <see cref="JsonPropertyNameAttribute"/> values (Cosmos / search / submit).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StreamingService
{
    [JsonPropertyName("bbcSounds")]
    [StreamingServiceInfo("BBC Sounds", "bbc-sounds", false, "bbc.co.uk", "bbc.com")]
    BbcSounds,

    [JsonPropertyName("bbcIplayer")]
    [StreamingServiceInfo("BBC iPlayer", "bbc-iplayer", true, "bbc.co.uk", "bbc.com")]
    BbcIplayer,

    [JsonPropertyName("internetArchive")]
    [StreamingServiceInfo("Internet Archive", "internet-archive", true, "archive.org")]
    InternetArchive,

    [JsonPropertyName("vimeo")]
    [StreamingServiceInfo("Vimeo", "vimeo", true, "vimeo.com")]
    Vimeo,

    [JsonPropertyName("netflix")]
    [StreamingServiceInfo("Netflix", "netflix", true, "netflix.com")]
    Netflix,

    [JsonPropertyName("amazonPrime")]
    [StreamingServiceInfo("Amazon Prime Video", "amazon-prime", true, "primevideo.com", "amazon.com", "amazon.co.uk")]
    AmazonPrime,

    [JsonPropertyName("paramountPlus")]
    [StreamingServiceInfo("Paramount+", "paramount-plus", true, "paramountplus.com")]
    ParamountPlus,

    [JsonPropertyName("hboMax")]
    [StreamingServiceInfo("HBO Max", "hbo-max", true, "max.com", "hbomax.com")]
    HboMax,

    [JsonPropertyName("playSuisse")]
    [StreamingServiceInfo("Play Suisse", "play-suisse", true, "playsuisse.ch")]
    PlaySuisse,

    [JsonPropertyName("playRts")]
    [StreamingServiceInfo("Play RTS", "play-rts", true, "rts.ch")]
    PlayRts,

    [JsonPropertyName("tvnzPlus")]
    [StreamingServiceInfo("TVNZ+", "tvnz-plus", true, "tvnz.co.nz")]
    TvnzPlus,

    [JsonPropertyName("itvx")]
    [StreamingServiceInfo("ITVX", "itvx", true, "itv.com")]
    Itvx,

    [JsonPropertyName("channel4")]
    [StreamingServiceInfo("Channel 4", "channel4", true, "channel4.com", "all4.com")]
    Channel4,

    [JsonPropertyName("fawesome")]
    [StreamingServiceInfo("Fawesome", "fawesome", true, "fawesome.tv")]
    Fawesome,

    [JsonPropertyName("disneyPlus")]
    [StreamingServiceInfo("Disney+", "disney-plus", true, "disneyplus.com")]
    DisneyPlus,

    [JsonPropertyName("bitchute")]
    [StreamingServiceInfo("BitChute", "bitchute", true, "bitchute.com")]
    BcVideo,

    [JsonPropertyName("tubi")]
    [StreamingServiceInfo("Tubi", "tubi", true, "tubitv.com")]
    Tubi,

    [JsonPropertyName("discoveryPlus")]
    [StreamingServiceInfo("discovery+", "discovery-plus", true, "discoveryplus.com")]
    DiscoveryPlus,

    [JsonPropertyName("franceTv")]
    [StreamingServiceInfo("France TV", "france-tv", true, "france.tv")]
    FranceTv
}
