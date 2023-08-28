using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public class CosmosDbKeySelector : ICosmosDbKeySelector
{
    public string GetKey(Podcast podcast)
    {
        return Enum.GetName(typeof(ModelType), podcast.ModelType)!;
    }
}