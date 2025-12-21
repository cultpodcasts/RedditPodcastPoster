using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Text.KnownTerms;

namespace RedditPodcastPoster.Text;

public partial class TextSanitiser(
    IKnownTermsProvider knownTermsProvider,
    ILogger<TextSanitiser> logger)
    : ITextSanitiser
{
    private static readonly Regex OApostrophe = CreateOApostrophe();
    private static readonly Regex HashtagOrAtSymbols = GenerateHashTagAtSymbolPatter();
    private static readonly Regex InQuotes = GenerateInQuotes();
    private static readonly Regex InvalidTitlePrefix = GenerateInvalidTitlePrefix();
    private static readonly Regex MultipleSpaces = GenerateMultipleSpaces();
    private static readonly Regex PostAsteriskLetters = GeneratePostAsteriskLetters();
    private static readonly Regex SeasonEpisode = GenerateSeasonEpisode();
    private static readonly TextInfo TextInfo = new CultureInfo("en-GB", false).TextInfo;

    public string SanitiseTitle(PostModel postModel)
    {
        return SanitiseTitle(postModel.EpisodeTitle, postModel.TitleRegex, postModel.PodcastKnownTerms,
            postModel.SubjectKnownTerms);
    }

    public string SanitisePodcastName(PostModel postModel)
    {
        return SanitisePodcastName(postModel.PodcastName);
    }

    public string SanitiseDescription(PostModel postModel)
    {
        return SanitiseDescription(postModel.EpisodeDescription, postModel.DescriptionRegex);
    }

    public string SanitiseTitle(string episodeTitle, Regex? regex, string[] podcastKnownTerms,
        string[] subjectKnownTerms)
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
        episodeTitle = RaiseOfApostropheLetter(episodeTitle);
        episodeTitle = LowerPostAsteriskLetters(episodeTitle);
        foreach (var term in LowerCaseTerms.Expressions)
        {
            episodeTitle = term.Value.Replace(episodeTitle, term.Key);
        }

        episodeTitle = FixCasing(episodeTitle, podcastKnownTerms, subjectKnownTerms);

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

    public string SanitiseDescription(string episodeDescription, Regex? descriptionRegex)
    {
        var description = ExtractDescription(episodeDescription, descriptionRegex);
        description = FixCharacters(description);
        return description;
    }

    public string ExtractDescription(string episodeDescription, string descriptionRegex)
    {
        if (!string.IsNullOrWhiteSpace(descriptionRegex))
        {
            var regex = new Regex(descriptionRegex!, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return ExtractDescription(episodeDescription, regex);
        }

        return episodeDescription;
    }

    public string ExtractDescription(string episodeDescription, Regex? descriptionRegex)
    {
        var description = Sanitise(episodeDescription);
        if (descriptionRegex != null)
        {
            description = ExtractBody(description, descriptionRegex);
        }

        return description;
    }

    public string RaiseOfApostropheLetter(string text)
    {
        var matches = OApostrophe.Matches(text);
        foreach (Match match in matches)
        {
            var index = match.Index;
            var length = match.Length;
            var pre = match.Groups["pre"].Value;
            var post = match.Groups["post"].Value;
            text = text.Substring(0, index) + pre + "'" + TextInfo.ToTitleCase(post.ToLower()) +
                   text.Substring(index + length);
        }

        return text;
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
        title = title.FixEntitles();
        title = title.Replace(@"""", "'");
        title = title.Replace(" and ", " & ");
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
        title = title.Replace(@"‘", "'");
        title = MultipleSpaces.Replace(title, " ");
        return title.Trim();
    }


    private string FixCasing(string input, string[] podcastKnownTerms, string[] subjectKnownTerms)
    {
        input = SeasonEpisode.Replace(input, m => m.Value.ToUpper());
        input = input.Replace("W/", "w/");
        var knownTerms = knownTermsProvider.GetKnownTerms();


        foreach (var term in knownTerms.Terms)
        {
            input = term.Value.Replace(input, term.Key);
        }

        foreach (var term in podcastKnownTerms.Select(x =>
                     new KeyValuePair<string, Regex>(x, new Regex($"\b{x}\b", RegexOptions.IgnoreCase))))
        {
            logger.LogInformation("Using podcast term '{podcastTerm}'.", term.Key);
            input = term.Value.Replace(input, term.Key);
        }

        foreach (var term in subjectKnownTerms.Select(x =>
                     new KeyValuePair<string, Regex>(x, new Regex($"\b{x}\b", RegexOptions.IgnoreCase))))
        {
            logger.LogInformation("Using subject term '{subjectTerm}'.", term.Key);
            input = term.Value.Replace(input, term.Key);
        }

        return input;
    }

    [GeneratedRegex(@"(?'prefix'^[^a-zA-Z\d""\$\£\'\(]+)(?'after'.*$)", RegexOptions.Compiled)]
    private static partial Regex GenerateInvalidTitlePrefix();

    [GeneratedRegex(@"[#@](\w+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-GB")]
    private static partial Regex GenerateHashTagAtSymbolPatter();

    [GeneratedRegex(@"^'(?'inquotes'.*)'$", RegexOptions.Compiled)]
    private static partial Regex GenerateInQuotes();

    [GeneratedRegex(@"\*(?'letter'[A-Z])", RegexOptions.Compiled)]
    private static partial Regex GeneratePostAsteriskLetters();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex GenerateMultipleSpaces();

    [GeneratedRegex(@"\b(?'pre'O)'\b(?'post'\w+)\b", RegexOptions.Compiled)]
    private static partial Regex CreateOApostrophe();

    [GeneratedRegex(@"\bS\d+ ?E\d+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GenerateSeasonEpisode();
}