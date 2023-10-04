using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public interface IKeySelector
{
    string GetKey(Podcast podcast);

    string GetKey(EliminationTerms.EliminationTerms eliminationTerms);

    string GetKey(KnownTerms.KnownTerms knownTerms);
}