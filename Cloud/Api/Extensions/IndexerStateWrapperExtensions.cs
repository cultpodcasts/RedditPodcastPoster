using RedditPodcastPoster.Search;
using IndexerState = Api.Dtos.IndexerState;

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