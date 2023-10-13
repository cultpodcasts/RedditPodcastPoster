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
        {"etc", new Regex(@"\betc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"to", new Regex(@"(?<!^)to\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"a", new Regex(@"(?<!^)a\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"an", new Regex(@"(?<!^)an\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"it", new Regex(@"(?<!^)it\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"not", new Regex(@"(?<!^)not\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"your", new Regex(@"(?<!^)your\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"you", new Regex(@"(?<!^)you\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"was", new Regex(@"(?<!^)was\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"isn't", new Regex(@"(?<!^)isn't\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"is", new Regex(@"(?<!^)is\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"want", new Regex(@"(?<!^)want\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"wants", new Regex(@"(?<!^)wants\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"her", new Regex(@"(?<!^)her\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"his", new Regex(@"(?<!^)his\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"from", new Regex(@"(?<!^)from\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"their", new Regex(@"(?<!^)their\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"they", new Regex(@"(?<!^)they\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"out", new Regex(@"(?<!^)out\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"come", new Regex(@"(?<!^)come\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"coming", new Regex(@"(?<!^)coming\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"away", new Regex(@"(?<!^)away\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"by", new Regex(@"(?<!^)by\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"what", new Regex(@"(?<!^)what\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"who", new Regex(@"(?<!^)who\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"made", new Regex(@"(?<!^)made\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"make", new Regex(@"(?<!^)make\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"since", new Regex(@"(?<!^)since\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"for", new Regex(@"(?<!^)for\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"go", new Regex(@"(?<!^)go\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"gone", new Regex(@"(?<!^)gone\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"give", new Regex(@"(?<!^)give\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"gives", new Regex(@"(?<!^)gives\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"given", new Regex(@"(?<!^)given\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"next", new Regex(@"(?<!^)next\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"with", new Regex(@"(?<!^)with\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"about", new Regex(@"(?<!^)about\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"how", new Regex(@"(?<!^)how\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"here", new Regex(@"(?<!^)here\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
    };

    private static readonly Regex Hashtag = new(@"\#(\w+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InvalidTitlePrefix =
        new(@"(?'prefix'^[^a-zA-Z\d""\$\£\']+)(?'after'.*$)", RegexOptions.Compiled);

    private static readonly TextInfo TextInfo = new CultureInfo("en-GB", false).TextInfo;
    private static readonly Regex WithName = new(@"(?'before'\s)(?'with'[Ww]ith )(?'after'[A-Z])");
    private readonly IKnownTermsProvider _knownTermsProvider;
    private readonly ILogger<TextSanitiser> _logger;

    public TextSanitiser(IKnownTermsProvider knownTermsProvider, ILogger<TextSanitiser> logger)
    {
        _knownTermsProvider = knownTermsProvider;
        _logger = logger;
    }

    public string SanitiseTitle(PostModel postModel)
    {
        return SanitiseTitle(postModel.EpisodeTitle, postModel.TitleRegex);
    }

    public string SanitisePodcastName(PostModel postModel)
    {
        return SanitisePodcastName(postModel.PodcastName);
    }

    public string SanitiseDescription(PostModel postModel)
    {
        return SanitiseDescription(postModel.EpisodeDescription, postModel.DescriptionRegex);
    }

    public string SanitiseTitle(string episodeTitle, Regex? regex)
    {
        if (regex != null)
        {
            episodeTitle = ExtractTitle(episodeTitle, regex);
        }

        episodeTitle = FixCharacters(episodeTitle);
        var withMatch = WithName.Match(episodeTitle).Groups["with"];
        if (withMatch.Success)
        {
            episodeTitle = WithName.Replace(episodeTitle, "${before}w/${after}");
        }

        var invalidPrefixMatch = InvalidTitlePrefix.Match(episodeTitle).Groups["prefix"];
        if (invalidPrefixMatch.Success)
        {
            episodeTitle = InvalidTitlePrefix.Replace(episodeTitle, "${after}");
        }

        episodeTitle = Hashtag.Replace(episodeTitle, "$1");
        episodeTitle = TextInfo.ToTitleCase(episodeTitle.ToLower());
        foreach (var term in TitleCaseTerms)
        {
            episodeTitle = term.Value.Replace(episodeTitle, term.Key);
        }
        episodeTitle = FixCasing(episodeTitle);

        episodeTitle = episodeTitle.Trim();
        return episodeTitle;
    }

    public string SanitisePodcastName(string podcastName)
    {
        podcastName = FixCharacters(podcastName);
        //podcastName = TextInfo.ToTitleCase(podcastName.ToLower());
        //podcastName = FixCasing(podcastName);
        return podcastName;
    }

    public string SanitiseDescription(string episodeDescription, Regex? regex)
    {
        var description = Sanitise(episodeDescription);
        if (regex != null)
        {
            description = ExtractBody(description, regex);
        }

        description = FixCharacters(description);
        return description;
    }


    internal string ExtractBody(string body, Regex regex)
    {
        var match = regex.Match(body);
        if (match.Success)
        {
            return match.Result("${body}");
        }

        return body;
    }

    internal string ExtractTitle(string episodeTitle, Regex regex)
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

    internal string Sanitise(string text)
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
        title = title.Replace(@"´", "'");
        return title;
    }

    private string FixCasing(string input)
    {
        input = input.Replace("W/", "w/");
        input = _knownTermsProvider.GetKnownTerms().MaintainKnownTerms(input);

        return input;
    }
}