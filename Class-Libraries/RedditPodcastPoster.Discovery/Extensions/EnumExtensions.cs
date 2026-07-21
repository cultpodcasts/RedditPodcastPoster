namespace RedditPodcastPoster.Discovery.Extensions;

public static class EnumExtensions
{
    public static TTo ConvertEnumByName<TFrom, TTo>(this TFrom value, bool ignoreCase = false)
        where TFrom : struct
        where TTo : struct
    {
        return Enum.Parse<TTo>(value.ToString()!, ignoreCase);
    }

    public static TTo? ConvertEnumByName<TFrom, TTo>(this TFrom? value, bool ignoreCase = false)
        where TFrom : struct
        where TTo : struct
    {
        if (value == null)
        {
            return null;
        }

        return ConvertEnumByName<TFrom, TTo>(value.Value, ignoreCase);
    }
}