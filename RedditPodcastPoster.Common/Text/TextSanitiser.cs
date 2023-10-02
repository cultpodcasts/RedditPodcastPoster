using System.Drawing.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Text;

public class TextSanitiser : ITextSanitiser
{
    private readonly Regex _invalidTitlePrefix = new(@"(?'prefix'^[^a-zA-Z\d""]+)(?'after'.*$)");
    private readonly TextInfo _textInfo = new CultureInfo("en-GB", false).TextInfo;
    private readonly Regex _withName = new(@"(?'before'\s)(?'with'[Ww]ith )(?'after'[A-Z])");

    public string ExtractBody(string body, Regex regex)
    {
        var match = regex.Match(body);
        if (match.Success)
        {
            return match.Result("${body}");
        }

        return body;
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

    public string SanitiseTitle(PostModel postModel)
    {
        var episodeTitle = postModel.EpisodeTitle;
        if (postModel.TitleRegex != null)
        {
            episodeTitle = ExtractTitle(episodeTitle, postModel.TitleRegex);
        }

        episodeTitle = FixCharacters(episodeTitle);
        var withMatch = _withName.Match(episodeTitle).Groups["with"];
        if (withMatch.Success)
        {
            episodeTitle = _withName.Replace(episodeTitle, "${before}w/${after}");
        }

        var invalidPrefixMatch = _invalidTitlePrefix.Match(episodeTitle).Groups["prefix"];
        if (invalidPrefixMatch.Success)
        {
            episodeTitle = _invalidTitlePrefix.Replace(episodeTitle, "${after}");
        }

        episodeTitle = _textInfo.ToTitleCase(episodeTitle.ToLower());
        episodeTitle = FixCasing(episodeTitle);
        return episodeTitle;
    }

    public string SanitisePodcastName(PostModel postModel)
    {
        var podcastName = postModel.PodcastName;
        podcastName = FixCharacters(podcastName);
        podcastName = _textInfo.ToTitleCase(podcastName.ToLower());
        return podcastName;
    }

    public string SanitiseDescription(PostModel postModel)
    {
        var description = Sanitise(postModel.EpisodeDescription);
        if (postModel.DescriptionRegex != null)
        {
            description = ExtractBody(description, postModel.DescriptionRegex);
        }

        description = FixCharacters(description);
        return description;
    }

    public string Sanitise(string text)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<body>" + text + "</body>");
        var innerText = doc.DocumentNode.SelectSingleNode("//body").InnerText;
        return innerText.Trim();
    }

    private string FixCharacters(string title)
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

    private string FixCasing(string input)
    {
        input = input.Replace("W/", "w/");
        input = KnownTerms.MaintainKnownTerms(input);
        
        return input;
    }
}