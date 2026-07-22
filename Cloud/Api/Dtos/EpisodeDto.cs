using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Dtos;

/// <summary>
/// Curate/admin episode JSON. Flat projection — does not inherit domain Episode.
/// </summary>
public class EpisodeDto
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastId")]
    [JsonPropertyOrder(2)]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(30)]
    public string PodcastName { get; set; } = "";

    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = "";

    [JsonPropertyName("displayTitle")]
    [JsonPropertyOrder(31)]
    public string DisplayTitle { get; set; } = "";

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = "";

    [JsonPropertyName("displayDescription")]
    [JsonPropertyOrder(41)]
    public string DisplayDescription { get; set; } = "";

    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(31)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(32)]
    public bool Explicit { get; set; }

    [JsonPropertyName("posted")]
    [JsonPropertyOrder(40)]
    public bool Posted { get; set; }

    [JsonPropertyName("tweeted")]
    [JsonPropertyOrder(41)]
    public bool Tweeted { get; set; }

    [JsonPropertyName("bluesky")]
    [JsonPropertyOrder(42)]
    public bool? BlueskyPosted { get; set; }

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(44)]
    public bool Removed { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(45)]
    public string? Language { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(50)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(51)]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(52)]
    public string YouTubeId { get; set; } = "";

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(60)]
    public ServiceUrls Urls { get; set; } = new();

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("removedSubjects")]
    [JsonPropertyOrder(71)]
    public List<string> RemovedSubjects { get; set; } = [];

    [JsonPropertyName("matches")]
    [JsonPropertyOrder(72)]
    public List<EpisodeSubjectMatch> Matches { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("images")]
    [JsonPropertyOrder(150)]
    public EpisodeImages? Images { get; set; }

    [JsonPropertyName("guests")]
    [JsonPropertyOrder(160)]
    public string[]? Guests { get; set; }

    [JsonPropertyName("youTubePodcast")]
    [JsonPropertyOrder(200)]
    public bool YouTubePodcast { get; set; }

    [JsonPropertyName("spotifyPodcast")]
    [JsonPropertyOrder(201)]
    public bool SpotifyPodcast { get; set; }

    [JsonPropertyName("applePodcast")]
    [JsonPropertyOrder(202)]
    public bool ApplePodcast { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(210)]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(211)]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(250)]
    public Uri? Image { get; set; }

    [JsonPropertyName("guestPeople")]
    [JsonPropertyOrder(270)]
    public IList<PersonDto>? GuestPeople { get; set; }

    [JsonPropertyName("guestSuggestions")]
    [JsonPropertyOrder(271)]
    public IList<PersonMatch>? GuestSuggestions { get; set; }

    public class PersonMatch
    {
        [JsonPropertyName("person")]
        public required PersonDto Person { get; set; }

        [JsonPropertyName("matchResults")]
        public required MatchResult[] MatchResults { get; set; }
    }

    public class MatchResult
    {
        [JsonPropertyName("term")]
        public required string Term { get; set; }

        [JsonPropertyName("matches")]
        public required int Matches { get; set; }
    }
}
