namespace RedditPodcastPoster.Models.Extensions;

public static class CosmosSelectorExtensions
{
    public static bool IsOfType<T>(this CosmosSelector x)
    {
        var customAttributes = typeof(T).GetCustomAttributes(typeof(CosmosSelectorAttribute), true);
        var typeModelType = ((CosmosSelectorAttribute)customAttributes.First()).ModelType;
        return x.ModelType == typeModelType;
    }
}