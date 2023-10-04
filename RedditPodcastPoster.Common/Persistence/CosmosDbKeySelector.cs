using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public class CosmosDbKeySelector : ICosmosDbKeySelector
{
    public string GetKey(Podcast podcast)
    {
        return Enum.GetName(typeof(ModelType), podcast.ModelType)!;
    }

    public string GetKey(EliminationTerms.EliminationTerms eliminationTerms)
    {
        return nameof(ModelType.EliminationTerms);
    }

    public string GetKey(KnownTerms.KnownTerms knownTerms)
    {
        return nameof(ModelType.KnownTerms);
    }
}