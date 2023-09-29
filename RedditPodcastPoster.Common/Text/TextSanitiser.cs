using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RedditPodcastPoster.Common.Text;

public class TextSanitiser : ITextSanitiser
{
    public string Sanitise(string text)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<body>" + text + "</body>");
        var innerText = doc.DocumentNode.SelectSingleNode("//body").InnerText;
        return innerText.Trim();
    }

    public string ExtractBody(string body, Regex regex)
    {
        var match = regex.Match(body);
        if (match.Success)
        {
            return match.Result("${body}");
        }

        return body;
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
        title = title.Replace("’", "'");
        return title;
    }

    public string FixCasing(string input)
    {
        input = input.Replace("W/", "w/");
        input = input.Replace(" The ", " the ");
        input = input.Replace(" Of ", " of ");
        input = input.Replace(" In ", " in ");
        input = input.Replace(" Bju ", " BJU ");
        input = input.Replace(" Jw ", " JW ");
        input = input.Replace(" Jws ", " JWs ");
        input = input.Replace(" Pbcc ", " PBCC ");
        input = input.Replace("Exjwhelp", "ExJWHelp");
        input = input.Replace(" Etc ", " etc ");
        return input;
    }

    public string ExtractTitle(string episodeTitle, Regex regex)
    {
        var match = regex.Match(episodeTitle);
        var replacement = "${title}";
        if (match.Groups["partsection"].Success)
        {
            replacement += " Pt.${partnumber}";
        }

        if (match.Success)
        {
            return match.Result(replacement);
        }
        return episodeTitle;
    }
}