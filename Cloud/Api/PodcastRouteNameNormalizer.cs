using System.Net;

namespace Api;

internal static class PodcastRouteNameNormalizer
{
    public static string Normalize(string routeName)
    {
        var name = Uri.UnescapeDataString(routeName);
        name = WebUtility.UrlDecode(name);
        return name.Trim();
    }
}
