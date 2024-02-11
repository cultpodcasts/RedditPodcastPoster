using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public static partial class EpisodeExtensions
{
    private static readonly Regex _multipleSpaces = GenerateMultipleSpaces();

    public static Uri GetUrl(this FullEpisode fullEpisode)
    {
        return new Uri(fullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute);
    }

    public static string GetDescription(this FullEpisode episode)
    {
        return GetDescription(episode.HtmlDescription);
    }

    public static string GetDescription(this SimpleEpisode episode)
    {
        return GetDescription(episode.HtmlDescription);
    }

    private static string GetDescription(string htmlDescription)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlDescription);
        return _multipleSpaces
            .Replace(GetReadableText(doc.DocumentNode), " ")
            .FixEntitles()
            .Trim();
    }

    private static string GetReadableText(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            return node.InnerText.Trim();
        }

        if (node.Name == "br")
        {
            return " ";
        }

        return string.Join(" ", node.ChildNodes.Select(GetReadableText));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex GenerateMultipleSpaces();
}