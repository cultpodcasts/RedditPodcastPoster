namespace RedditPodcastPoster.Discovery;

public class DiscoveryResult
{
    public Uri? Url { get; set; }
    public string? EpisodeName { get; set; }
    public string? ShowName { get; set; }
    public string? Description { get; set; }
    public DateTime Released { get; set; }
    public TimeSpan? Length { get; set; }
    public IEnumerable<string> Subjects { get; set; }= Enumerable.Empty<string>();
    public ulong? Views { get; set; }
    public ulong? MemberCount { get; set; }
}