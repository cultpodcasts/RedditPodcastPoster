using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.KnownTerms;
using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Text;

public class TextSanitiser : ITextSanitiser
{
    private static readonly Dictionary<string, Regex> TitleCaseTerms = new()
    {
        {"the", new Regex(@"(?<!^)the\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"of", new Regex(@"(?<!^)of\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"on", new Regex(@"(?<!^)on\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"in", new Regex(@"(?<!^)in\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"etc", new Regex(@"\betc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)}
    };

    private static readonly Regex _hashtag = new(@"\#(\w+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _invalidTitlePrefix = new(@"(?'prefix'^[^a-zA-Z\d""]+)(?'after'.*$)");
    private static readonly TextInfo _textInfo = new CultureInfo("en-GB", false).TextInfo;
    private static readonly Regex _withName = new(@"(?'before'\s)(?'with'[Ww]ith )(?'after'[A-Z])");
    private readonly IKnownTermsProvider _knownTermsProvider;
    private readonly ILogger<TextSanitiser> _logger;

    public TextSanitiser(IKnownTermsProvider knownTermsProvider, ILogger<TextSanitiser> logger)
    {
        _knownTermsProvider = knownTermsProvider;
        _logger = logger;
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
        episodeTitle = _hashtag.Replace(episodeTitle, "$1");
        foreach (var term in TitleCaseTerms)
        {
            episodeTitle = term.Value.Replace(episodeTitle, term.Key);
        }

        episodeTitle = episodeTitle.Trim();
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
        input = _knownTermsProvider.GetKnownTerms().MaintainKnownTerms(input);

        return input;
    }
}