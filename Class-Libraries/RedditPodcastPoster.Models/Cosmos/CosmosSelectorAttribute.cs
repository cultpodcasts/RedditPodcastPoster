namespace RedditPodcastPoster.Models.Cosmos;

public class CosmosSelectorAttribute(ModelType modelType) : Attribute
{
    public ModelType ModelType { get; init; } = modelType;
}
