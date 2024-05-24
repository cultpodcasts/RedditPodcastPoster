namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class EpisodeResult(
    string id,
    DateTime released,
    string description,
    string episodeName,
    TimeSpan? length,
    string showName,
    DiscoverService discoverService,
    Uri? url = null,
    string? servicePodcastId = null,
    ulong? viewCount = null,
    ulong? memberCount = null,
    Uri? imageUrl = null)
{
    public string Id { get; init; } = id;
    public DateTime Released { get; set; } = released;
    public string Description { get; init; } = description;
    public string EpisodeName { get; init; } = episodeName;
    public TimeSpan? Length { get; init; } = length;
    public string ShowName { get; init; } = showName;
    public DiscoverService DiscoverService { get; set; } = discoverService;
    public Uri? Url { get; set; } = url;
    public string? ServicePodcastId { get; set; } = servicePodcastId;
    public ulong? ViewCount { get; init; } = viewCount;
    public ulong? MemberCount { get; init; } = memberCount;
    public Uri? ImageUrl { get; set; } = imageUrl;

    public void Deconstruct(out string Id, out DateTime Released, out string Description, out string EpisodeName,
        out TimeSpan? Length, out string ShowName, out DiscoverService DiscoverService, out Uri? Url,
        out string? ServicePodcastId, out ulong? ViewCount, out ulong? MemberCount, out Uri? ImageUrl)
    {
        Id = this.Id;
        Released = this.Released;
        Description = this.Description;
        EpisodeName = this.EpisodeName;
        Length = this.Length;
        ShowName = this.ShowName;
        DiscoverService = this.DiscoverService;
        Url = this.Url;
        ServicePodcastId = this.ServicePodcastId;
        ViewCount = this.ViewCount;
        MemberCount = this.MemberCount;
        ImageUrl = this.ImageUrl;
    }
}