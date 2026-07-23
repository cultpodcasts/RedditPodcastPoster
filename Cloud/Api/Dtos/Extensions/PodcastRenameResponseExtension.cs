using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Dtos.Extensions;

public static class PodcastRenameResponseExtension
{
    public static PodcastRenameResponse ToPodcastRenameResponse(this EntitySearchIndexerResponse indexed)
    {
        return new PodcastRenameResponse { IndexState = indexed.ToDto() };
    }
}
