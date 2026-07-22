using Api.Extensions;
using Api.Models;

namespace Api.Dtos.Extensions;

public static class EpisodeUpdateResponseExtension
{
    public static EpisodeUpdateResponse ToDto(this EpisodeUpdateOutcome outcome)
    {
        return new EpisodeUpdateResponse
        {
            TweetDeleted = outcome.TweetDeleted,
            BlueskyPostDeleted = outcome.BlueskyPostDeleted,
            SearchIndexerState = outcome.SearchIndexer?.ToDto()
        };
    }
}
