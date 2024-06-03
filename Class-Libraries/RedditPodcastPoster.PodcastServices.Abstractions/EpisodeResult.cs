namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class EpisodeResult(
    string id,
    DateTime released,
    string description,
    string episodeName,
    TimeSpan? length,
    string showName,
    DiscoverService discoverService,
    string? servicePodcastId = null,
    ulong? viewCount = null,
    ulong? memberCount = null,
    Uri? imageUrl = null,
    long? itunesPodcastId = null)
{
    public long? ITunesPodcastId = itunesPodcastId;
    public string Id { get; init; } = id;
    public DateTime Released { get; set; } = released;
    public string Description { get; init; } = description;
    public string EpisodeName { get; init; } = episodeName;
    public TimeSpan? Length { get; init; } = length;
    public string ShowName { get; init; } = showName;
    public DiscoverService DiscoverService { get; set; } = discoverService;
    public EnrichmentService? EnrichedFrom { get; set; }
    public string? ServicePodcastId { get; set; } = servicePodcastId;
    public ulong? ViewCount { get; init; } = viewCount;
    public ulong? MemberCount { get; init; } = memberCount;
    public Uri? ImageUrl { get; set; } = imageUrl;
    public PodcastServiceUrls Urls { get; set; } = new();
}