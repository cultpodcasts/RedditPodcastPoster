namespace Api.Models;

internal static class PodcastRouteNameNormalizer
{
    public static string Normalize(string routeName)
    {
        var name = DecodePercentEncodingPreservingPlus(routeName);
        if (name.Contains('%', StringComparison.Ordinal))
        {
            name = DecodePercentEncodingPreservingPlus(name);
        }

        return name.Trim();
    }

    /// <summary>
    /// Percent-decode a route segment without treating '+' as space.
    /// <see cref="System.Net.WebUtility.UrlDecode"/> is form-urlencoded and would turn
    /// names like "News+Weather" into "News Weather".
    /// </summary>
    private static string DecodePercentEncodingPreservingPlus(string value) =>
        Uri.UnescapeDataString(value.Replace("+", "%2B", StringComparison.Ordinal));
}
