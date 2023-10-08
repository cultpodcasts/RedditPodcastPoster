using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public interface IPartitionKeySelector
{
    string GetKey(Podcast podcast);

    string GetKey(EliminationTerms.EliminationTerms eliminationTerms);

    string GetKey(KnownTerms.KnownTerms knownTerms);
}