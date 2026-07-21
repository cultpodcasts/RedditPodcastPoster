using IndexerState = Api.Dtos.IndexerState;
using RedditPodcastPoster.Search.Models;

namespace Api.Extensions;

public static class IndexerStateWrapperExtensions
{
    public static IndexerState ToDto(this IndexerStateWrapper indexerStateWrapper)
    {
        return new IndexerState
        {
            State = indexerStateWrapper.IndexerState,
            NextRun = indexerStateWrapper.NextRun,
            LastRan = indexerStateWrapper.LastRan?.Duration()
        };
    }
}