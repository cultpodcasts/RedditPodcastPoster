using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Text.Sanitisers;

public partial class TextSanitiser(
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProviderInstance)
    : ITextSanitiser
{
    private static readonly Regex OApostrophe = CreateOApostrophe();
    private static readonly Regex RomanceElisionApostrophe = CreateRomanceElisionApostrophe();
    private static readonly Regex McMacPrefix = CreateMcMacPrefix();
    private static readonly Regex HashtagOrAtSymbols = GenerateHashTagAtSymbolPatter();
    private static readonly Regex InQuotes = GenerateInQuotes();
    private static readonly Regex InvalidTitlePrefix = GenerateInvalidTitlePrefix();
    private static readonly Regex MultipleSpaces = GenerateMultipleSpaces();
    private static readonly Regex PostAsteriskLetters = GeneratePostAsteriskLetters();
    private static readonly Regex SeasonEpisode = GenerateSeasonEpisode();
    private static readonly TextInfo TextInfo = new CultureInfo("en-GB", false).TextInfo;

    public async Task<string> SanitiseTitle(PostModel postModel)
    {
        return await SanitiseTitle(postModel.EpisodeTitle, postModel.TitleRegex, postModel.PodcastKnownTerms,
            postModel.SubjectKnownTerms, postModel.Language);
    }

    public string SanitisePodcastName(PostModel postModel)
    {
        return SanitisePodcastName(postModel.PodcastName);
    }

    public string SanitiseDescription(PostModel postModel)
    {
        return SanitiseDescription(postModel.EpisodeDescription, postModel.DescriptionRegex);
    }

    public async Task<string> SanitiseTitle(string episodeTitle, Regex? regex, string[] podcastKnownTerms,
        string[] subjectKnownTerms, string? language = null)
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
        episodeTitle = FixRomanceElisionApostrophe(episodeTitle);
        episodeTitle = FixMcMacPrefix(episodeTitle);
        episodeTitle = LowerPostAsteriskLetters(episodeTitle);

        var rulesProvider = await titleCasingRulesProviderInstance.GetAsync();
        foreach (var term in rulesProvider.GetLowerCaseExpressions(language))
        {
            episodeTitle = term.Value.Replace(episodeTitle, term.Key);
        }

        episodeTitle = FixCasing(episodeTitle, podcastKnownTerms, subjectKnownTerms, language, rulesProvider);

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

    public string FixRomanceElisionApostrophe(string text)
    {
        var matches = RomanceElisionApostrophe.Matches(text);
        foreach (Match match in matches)
        {
            var index = match.Index;
            var length = match.Length;
            var pre = match.Groups["pre"].Value.ToLower();
            var post = match.Groups["post"].Value;
            text = text.Substring(0, index) + pre + "'" + TextInfo.ToTitleCase(post.ToLower()) +
                   text.Substring(index + length);
        }

        return text;
    }

    /// <summary>
    /// Recapitalises the letter after <c>Mc</c> always, and after <c>Mac</c> only when the
    /// token is a known surname in <see cref="MacSurnames"/> (e.g. Mcewan → McEwan,
    /// Macewan → MacEwan). Leaves English Mac* words alone (Machine, Machination, …).
    /// </summary>
    public string FixMcMacPrefix(string text)
    {
        var matches = McMacPrefix.Matches(text);
        foreach (Match match in matches.Cast<Match>().OrderByDescending(m => m.Index))
        {
            var prefix = match.Groups["prefix"].Value;
            var rest = match.Groups["rest"].Value;
            if (prefix.Equals("Mac", StringComparison.Ordinal) &&
                !MacSurnames.Names.Contains(match.Value))
            {
                continue;
            }

            var fixedWord = prefix + char.ToUpperInvariant(rest[0]) + rest[1..];
            text = text.Substring(0, match.Index) + fixedWord + text.Substring(match.Index + match.Length);
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
        title = title.Replace("ΓÇ£", "'");
        title = title.Replace("ΓÇ¥", "'");
        title = title.Replace("ΓÇÖ", "'");
        title = title.Replace(@"┬┤", "'");
        title = title.Replace(@"ΓÇÿ", "'");
        title = MultipleSpaces.Replace(title, " ");
        return title.Trim();
    }

    private string FixCasing(
        string input,
        string[] podcastKnownTerms,
        string[] subjectKnownTerms,
        string? language,
        ITitleCasingRulesProvider rulesProvider)
    {
        input = SeasonEpisode.Replace(input, m => m.Value.ToUpper());
        input = input.Replace("W/", "w/");

        foreach (var term in rulesProvider.GetUniversalKnownTerms())
        {
            input = term.ToRegex().Replace(input, term.Literal);
        }

        foreach (var term in rulesProvider.GetKnownTerms(language))
        {
            input = term.ToRegex().Replace(input, term.Literal);
        }

        foreach (var term in podcastKnownTerms.Select(x =>
                     new KeyValuePair<string, Regex>(x, new Regex($@"\b{x}\b", RegexOptions.IgnoreCase))))
        {
            input = term.Value.Replace(input, term.Key);
        }

        foreach (var term in subjectKnownTerms.Select(x =>
                     new KeyValuePair<string, Regex>(x, new Regex($@"\b{x}\b", RegexOptions.IgnoreCase))))
        {
            input = term.Value.Replace(input, term.Key);
        }

        return input;
    }

    [GeneratedRegex(@"(?'prefix'^[^\p{L}\p{N}""\$\u00A3\'\(]+)(?'after'.*$)", RegexOptions.Compiled)]
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

    [GeneratedRegex(@"\b(?'pre'[LlDd])'\b(?'post'\w+)\b", RegexOptions.Compiled)]
    private static partial Regex CreateRomanceElisionApostrophe();

    // Mac before Mc so "Macewan" matches Mac, not a leading Mc fragment.
    [GeneratedRegex(@"\b(?'prefix'Mac|Mc)(?'rest'[a-z]\w*)\b", RegexOptions.Compiled)]
    private static partial Regex CreateMcMacPrefix();

    [GeneratedRegex(@"\bS\d+ ?E\d+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GenerateSeasonEpisode();
}
