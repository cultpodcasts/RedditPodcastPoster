namespace RedditPodcastPoster.Models;

public class CosmosSelectorAttribute : Attribute
{
    public  ModelType ModelType { get; init; }

    public CosmosSelectorAttribute(ModelType modelType)
    {
        ModelType = modelType;
    }
}