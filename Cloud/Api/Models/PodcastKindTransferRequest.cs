using RedditPodcastPoster.Models.Catalogue;

namespace Api.Models;

public class PodcastKindTransferRequest
{
    public CatalogueParentKind? TargetKind { get; set; }
}
