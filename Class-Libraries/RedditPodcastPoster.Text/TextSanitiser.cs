using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Text.KnownTerms;

namespace RedditPodcastPoster.Text;

public partial class TextSanitiser(IKnownTermsProvider knownTermsProvider, ILogger<TextSanitiser> logger)
    : ITextSanitiser
{
    private static readonly Regex HashtagOrAtSymbols = GenerateHashTagAtSymbolPatter();
    private static readonly Regex InQuotes = GenerateInQuotes();
    private static readonly Regex InvalidTitlePrefix = GenerateInvalidTitlePrefix();

    private static readonly TextInfo TextInfo = new CultureInfo("en-GB", false).TextInfo;
    private static readonly Regex PostAsteriskLetters = GeneratePostAsteriskLetters();
    private readonly ILogger<TextSanitiser> _logger = logger;

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

        var invalidPrefixMatch = InvalidTitlePrefix.Match(episodeTitle).Groups["prefix"];
        if (invalidPrefixMatch.Success)
        {
            episodeTitle = InvalidTitlePrefix.Replace(episodeTitle, "${after}");
        }

        episodeTitle = HashtagOrAtSymbols.Replace(episodeTitle, "$1");
        episodeTitle = TextInfo.ToTitleCase(episodeTitle.ToLower());
        episodeTitle = LowerPostAsteriskLetters(episodeTitle);
        foreach (var term in LowerCaseTerms.Expressions)
        {
            episodeTitle = term.Value.Replace(episodeTitle, term.Key);
        }

        episodeTitle = FixCasing(episodeTitle);

        episodeTitle = episodeTitle.Trim();
        var inQuotesMatch = InQuotes.Match(episodeTitle);
        if (inQuotesMatch.Success)
        {
            episodeTitle = inQuotesMatch.Groups["inquotes"].Value;
        }

        return episodeTitle;
    }

    public string SanitisePodcastName(string podcastName)
    {
        podcastName = FixCharacters(podcastName);
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

    private string LowerPostAsteriskLetters(string text)
    {
        var matches = PostAsteriskLetters.Matches(text);
        foreach (Match match in matches)
        {
            var index = match.Index;
            var length = match.Length;
            var asterisks = new string('*', length - 1);
            var character = match.Groups["letter"].Value;
            text = text.Substring(0, index) + asterisks + character.ToLower() + text.Substring(index + length);
        }

        return text;
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
        title = title.Replace("\n", " ");
        title = title.Replace("\r", " ");
        title = title.Replace("  ", " ");
        return title.Trim();
    }

    private string FixCasing(string input)
    {
        input = input.Replace("W/", "w/");
        input = knownTermsProvider.GetKnownTerms().MaintainKnownTerms(input);

        return input;
    }

    [GeneratedRegex("(?'prefix'^[^a-zA-Z\\d\"\\$\\£\\'\\(]+)(?'after'.*$)", RegexOptions.Compiled)]
    private static partial Regex GenerateInvalidTitlePrefix();

    [GeneratedRegex("[#@](\\w+)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-GB")]
    private static partial Regex GenerateHashTagAtSymbolPatter();

    [GeneratedRegex("^'(?'inquotes'.*)'$", RegexOptions.Compiled)]
    private static partial Regex GenerateInQuotes();

    [GeneratedRegex(@"\*(?'letter'[A-Z])", RegexOptions.Compiled)]
    private static partial Regex GeneratePostAsteriskLetters();
}