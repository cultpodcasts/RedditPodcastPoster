namespace RedditPodcastPoster.Models;

public class CosmosSelectorAttribute : Attribute
{
    public CosmosSelectorAttribute(ModelType modelType)
    {
        ModelType = modelType;
    }

    public ModelType ModelType { get; init; }
}