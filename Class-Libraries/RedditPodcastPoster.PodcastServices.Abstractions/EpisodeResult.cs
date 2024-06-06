namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class EpisodeResult(
    string id,
    DateTime released,
    string description,
    string episodeName,
    TimeSpan? length,
    string showName,
    DiscoverService discoverService,
    ulong? viewCount = null,
    ulong? memberCount = null,
    Uri? imageUrl = null,
    long? itunesPodcastId = null)
{
    public long? ITunesPodcastId { get; init; } = itunesPodcastId;
    public string Id { get; init; } = id;
    public DateTime Released { get; set; } = released;
    public string Description { get; init; } = description;
    public string EpisodeName { get; init; } = episodeName;
    public TimeSpan? Length { get; init; } = length;
    public string ShowName { get; init; } = showName;
    public DiscoverService[] DiscoverServices { get; set; } = [discoverService];
    public ulong? ViewCount { get; set; } = viewCount;
    public ulong? MemberCount { get; set; } = memberCount;
    public Uri? ImageUrl { get; set; } = imageUrl;
    public PodcastServiceUrls Urls { get; set; } = new();
    public PodcastServiceIds PodcastIds { get; set; } = new();
    public bool EnrichedTimeFromApple { get; set; }
    public bool EnrichedUrlFromSpotify { get; set; }
}