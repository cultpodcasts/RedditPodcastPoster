namespace RedditPodcastPoster.Models.Converters;

public static class TypeExtensions
{
    public static IEnumerable<Type> GetInterfacesAndSelf(this Type type)
    {
        return (type ?? throw new ArgumentNullException()).IsInterface
            ? new[] {type}.Concat(type.GetInterfaces())
            : type.GetInterfaces();
    }
}