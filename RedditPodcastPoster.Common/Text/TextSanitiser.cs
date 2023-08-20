using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Common.Text;

public class TextSanitiser : ITextSanitiser
{
    public string Sanitise(string text)
    {
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml("<body>"+text+"</body>");
        var innerText = doc.DocumentNode.SelectSingleNode("//body").InnerText;
        return innerText.Trim();
    }

    public string ExtractBody(string body, Regex regex)
    {
        var match = regex.Match(body);
        return match.Result("${body}");
    }

    public string FixCharacters(string title)
    {
        title = title.Replace("&apos;", "'");
        title = title.Replace("&quot;", "'");
        title = title.Replace("&amp;", "&");
        title = title.Replace("&#8217;", "'");
        title = title.Replace("&#39;", "'");
        title = title.Replace("**", "*");
        title = title.Replace(@"""", "'");
        title = title.Replace(" and ", " & ");
        title = title.Replace(" one ", " 1 ");
        title = title.Replace(" two ", " 2 ");
        title = title.Replace(" three ", " 3 ");
        title = title.Replace(" four ", " 4 ");
        title = title.Replace(" five ", " 5 ");
        title = title.Replace(" six ", " 6 ");
        title = title.Replace(" seven ", " 7 ");
        title = title.Replace(" eight ", " 8 ");
        title = title.Replace(" nine ", " 9 ");
        title = title.Replace(" ", " ");
        title = title.Replace("“", "'");
        title = title.Replace("”", "'");
        return title;
    }

    public string ExtractTitle(string episodeTitle, Regex regex)
    {
        var match = regex.Match(episodeTitle);
        var replacement = "${title}";
        if (match.Groups["partsection"].Success)
        {
            replacement += " Pt.${partnumber}";
        }
        return match.Result(replacement);
    }

}