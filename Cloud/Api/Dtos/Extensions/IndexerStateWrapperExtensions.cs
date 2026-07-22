using RedditPodcastPoster.Search.Models;

namespace Api.Dtos.Extensions;

public static class IndexerStateWrapperExtensions
{
    public static IndexerStateDto ToDto(this IndexerStateWrapper indexerStateWrapper)
    {
        return new IndexerStateDto
        {
            State = indexerStateWrapper.IndexerState,
            NextRun = indexerStateWrapper.NextRun,
            LastRan = indexerStateWrapper.LastRan?.Duration()
        };
    }
}
