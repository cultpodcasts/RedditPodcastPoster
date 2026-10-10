using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Converters;

namespace Api.Dtos;

public class DiscoveryResponse
{
    [JsonPropertyName("ids")]
    public required IEnumerable<Guid> Ids { get; set; }

    [JsonPropertyName("results")]
    public required IEnumerable<Item> Results { get; set; }

    [JsonPropertyName("hiddenCount")]
    public int HiddenCount { get; set; }

    public class Item
    {
        [JsonPropertyName("id")]
        [JsonPropertyOrder(10)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("episodeName")]
        [JsonPropertyOrder(20)]
        public string? EpisodeName { get; set; }

        [JsonPropertyName("showName")]
        [JsonPropertyOrder(30)]
        public string? ShowName { get; set; }

        [JsonPropertyName("episodeDescription")]
        [JsonPropertyOrder(40)]
        public string? Description { get; set; }

        [JsonPropertyName("showDescription")]
        [JsonPropertyOrder(41)]
        public string? ShowDescription { get; set; }

        [JsonPropertyName("released")]
        [JsonPropertyOrder(50)]
        public DateTime Released { get; set; }

        [JsonPropertyName("duration")]
        [JsonPropertyOrder(60)]
        public TimeSpan? Length { get; set; }

        [JsonPropertyName("urls")]
        [JsonPropertyOrder(70)]
        public DiscoveryResultUrlsDto Urls { get; set; } = new();

        [JsonPropertyName("subjects")]
        [JsonPropertyOrder(80)]
        public IEnumerable<string> Subjects { get; set; } = [];

        [JsonPropertyName("subjectMatches")]
        [JsonPropertyOrder(85)]
        /// <summary>
        /// Why each subject matched. <c>null</c> for historic results recorded before provenance
        /// existed (serialised as <c>"subjectMatches": null</c>); <c>[]</c> when recorded but empty.
        /// </summary>
        public IEnumerable<SubjectMatchDto>? SubjectMatches { get; set; }

        [JsonPropertyName("youTubeViews")]
        [JsonPropertyOrder(90)]
        public ulong? YouTubeViews { get; set; }

        [JsonPropertyName("youTubeChannelMembers")]
        [JsonPropertyOrder(100)]
        public ulong? YouTubeChannelMembers { get; set; }

        [JsonPropertyName("containsSyntheticMedia")]
        [JsonPropertyOrder(105)]
        public bool? ContainsSyntheticMedia { get; set; }

        [JsonPropertyName("guests")]
        [JsonPropertyOrder(108)]
        public string[] Guests { get; set; } = [];

        [JsonPropertyName("imageUrl")]
        [JsonPropertyOrder(110)]
        public Uri? ImageUrl { get; set; }

        [JsonPropertyName("discoverService")]
        [JsonConverter(typeof(ItemConverterDecorator<JsonStringEnumConverter>))]
        [JsonPropertyOrder(120)]
        public DiscoverService[] Sources { get; set; } = [];

        [JsonPropertyName("enrichedTimeFromApple")]
        [JsonPropertyOrder(130)]
        public bool EnrichedTimeFromApple { get; set; }

        [JsonPropertyName("enrichedUrlFromSpotify")]
        [JsonPropertyOrder(140)]
        public bool EnrichedUrlFromSpotify { get; set; }

        [JsonPropertyName("matchingPodcasts")]
        [JsonPropertyOrder(150)]
        public MatchingPodcast[]? MatchingPodcasts { get; set; }

        [JsonPropertyName("acceptProbability")]
        [JsonPropertyOrder(160)]
        public float? AcceptProbability { get; set; }

        [JsonPropertyName("autoHidden")]
        [JsonPropertyOrder(170)]
        public bool AutoHidden { get; set; }

        public enum DiscoverService
        {
            Spotify = 1,
            ListenNotes,
            YouTube,
            Taddy
        }

        public class MatchingPodcast
        {
            [JsonPropertyName("name")]
            public required string Name { get; set; }

            [JsonPropertyName("visible")]
            public required bool IsVisible { get; set; }

            [JsonPropertyName("visibleEpisodes")]
            public required int VisibleEpisodes { get; set; }
        }
    }
}
