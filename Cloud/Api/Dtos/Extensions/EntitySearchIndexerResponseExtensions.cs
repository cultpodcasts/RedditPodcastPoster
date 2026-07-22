using DomainIndexerState = RedditPodcastPoster.Search.Models.IndexerState;
using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Dtos.Extensions;

public static class EntitySearchIndexerResponseExtensions
{
    public static SearchIndexerState ToDto(this EntitySearchIndexerResponse response)
    {
        if (response.IndexerState != null)
        {
            switch (response.IndexerState)
            {
                case DomainIndexerState.Failure:
                    return SearchIndexerState.Failure;
                case DomainIndexerState.AlreadyRunning:
                    return SearchIndexerState.AlreadyRunning;
                case DomainIndexerState.TooManyRequests:
                    return SearchIndexerState.TooManyRequests;
                case DomainIndexerState.Executed:
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
