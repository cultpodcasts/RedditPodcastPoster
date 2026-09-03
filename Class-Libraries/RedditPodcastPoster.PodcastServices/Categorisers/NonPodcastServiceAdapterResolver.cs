using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

namespace RedditPodcastPoster.PodcastServices.Categorisers;

public class NonPodcastServiceAdapterResolver(
    IEnumerable<INonPodcastServiceAdapter> adapters
) : INonPodcastServiceAdapterResolver
{
    public INonPodcastServiceAdapter? ForSubmit(Uri url) =>
        adapters.FirstOrDefault(adapter => adapter.IsSubmitUrl(url));

    public INonPodcastServiceAdapter? ForExtract(Uri url) =>
        adapters.FirstOrDefault(adapter => adapter.CanExtract(url));
}
