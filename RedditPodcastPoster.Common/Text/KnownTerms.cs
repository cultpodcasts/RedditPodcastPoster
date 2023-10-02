using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Common.Text;

public static class KnownTerms
{
    private static readonly Dictionary<string, Regex> Terms = new()
    {
        {"the", new Regex(@"(?<!^)the\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"of", new Regex(@"(?<!^)of\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"on", new Regex(@"(?<!^)on\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"in", new Regex(@"(?<!^)in\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"JWs", new Regex(@"\bJWs\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"etc", new Regex(@"\betc\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"ABCs", new Regex(@"\bABCs\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"ExJWHelp", new Regex(@"\bExJWHelp\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"PBCC", new Regex(@"\bPBCC\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"BJU", new Regex(@"\bBJU\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"JW", new Regex(@"\bJW\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"IBLP", new Regex(@"\bIBLP\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"EDUCO", new Regex(@"\bEDUCO\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"NXIVM", new Regex(@"\bNXIVM\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
    };

    public static string MaintainKnownTerms(string input)
    {
        foreach (var term in Terms)
        {
            input = term.Value.Replace(input, term.Key);
        }

        return input;
    }
}