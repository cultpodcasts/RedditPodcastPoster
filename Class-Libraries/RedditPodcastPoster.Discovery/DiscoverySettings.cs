namespace RedditPodcastPoster.Discovery;

public class DiscoverySettings
{
    public IEnumerable<ServiceConfig>? Queries { get; set; }


    public override string ToString()
    {
        if (Queries == null)
        {
            return $"No {nameof(ServiceConfig)} found in configuration";
        }

        var items = Queries.Select(x => $"\"{x.Term}\" ({x.DiscoverService.ToString()})");
        return string.Join(", ", items);
    }
}