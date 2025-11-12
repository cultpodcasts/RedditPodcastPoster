using Api.Dtos;
using RedditPodcastPoster.EntitySearchIndexer;
using IndexerState = RedditPodcastPoster.Search.IndexerState;

namespace Api.Extensions;

public static class EntitySearchIndexerResponseExtensions
{
    public static SearchIndexerState ToDto(this EntitySearchIndexerResponse response)
    {
        if (response.IndexerState != null)
        {
            switch (response.IndexerState)
            {
                case IndexerState.Failure:
                    return SearchIndexerState.Failure;
                case IndexerState.AlreadyRunning:
                    return SearchIndexerState.AlreadyRunning;
                case IndexerState.TooManyRequests:
                    return SearchIndexerState.TooManyRequests;
                case IndexerState.Executed:
                    return SearchIndexerState.Executed;
            }
        }
        else if (response.EpisodeIndexRequestState != null)
        {
            switch (response.EpisodeIndexRequestState)
            {
                case EpisodeIndexRequestState.EpisodeIdConflict:
                    return SearchIndexerState.EpisodeIdConflict;
                case EpisodeIndexRequestState.EpisodeNotFound:
                    return SearchIndexerState.EpisodeNotFound;
                case EpisodeIndexRequestState.NoDocuments:
                    return SearchIndexerState.NoDocuments;
            }
        }

        return SearchIndexerState.Unknown;
    }
}