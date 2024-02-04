namespace RedditPodcastPoster.Models.Extensions;

public static class CosmosSelectorExtensions
{
    public static bool IsOfType<T>(this T x) where T : CosmosSelector
    {
        var customAttributes = typeof(T).GetCustomAttributes(typeof(CosmosSelectorAttribute), true);
        var typeModelType = ((CosmosSelectorAttribute) customAttributes.First()).ModelType;
        return x.ModelType == typeModelType;
    }

    public static ModelType GetModelType<T>() where T : CosmosSelector
    {
        var customAttributes = typeof(T).GetCustomAttributes(typeof(CosmosSelectorAttribute), true);
        var typeModelType = ((CosmosSelectorAttribute) customAttributes.First()).ModelType;
        return typeModelType;
    }
}