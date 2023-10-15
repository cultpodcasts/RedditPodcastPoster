namespace RedditPodcastPoster.Common.Extensions;

public static class DictionaryExtensions
{
    public static IDictionary<T1, T2> AddRange<T1, T2>(this IDictionary<T1, T2> target, IDictionary<T1, T2> source)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        foreach (var element in source)
        {
            target.Add(element);
        }

        return target;
    }
}