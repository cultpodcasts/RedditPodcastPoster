using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public class PartitionKeySelector : IPartitionKeySelector
{
    public string GetKey(Podcast podcast)
    {
        return podcast.ModelType.ToString();
    }

    public string GetKey(EliminationTerms.EliminationTerms eliminationTerms)
    {
        return eliminationTerms.ModelType.ToString();
    }

    public string GetKey(KnownTerms.KnownTerms knownTerms)
    {
        return knownTerms.ModelType.ToString();
    }

}