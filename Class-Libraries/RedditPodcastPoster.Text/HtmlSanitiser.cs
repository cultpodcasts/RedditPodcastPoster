using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Text;

public partial class HtmlSanitiser(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<HtmlSanitiser> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IHtmlSanitiser
{
    private readonly Regex _multipleSpaces = GenerateMultipleSpacesRegex();

    public string Sanitise(string htmlDescription)
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

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex GenerateMultipleSpacesRegex();
}