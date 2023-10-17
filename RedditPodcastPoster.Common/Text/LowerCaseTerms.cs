using System.Text.RegularExpressions;
using RedditPodcastPoster.Common.Extensions;

namespace RedditPodcastPoster.Common.Text;

public static class LowerCaseTerms
{
    private static readonly string[] LesserWords =
    {
        "the", "of", "on", "in", "to", "a", "an", "it", "not", "your", "you", "was", "isn't", "is", "want", "wants",
        "her", "his", "from", "their", "they", "out", "come", "coming", "away", "by", "what", "who", "made", "make",
        "since", "for", "go", "gone", "give", "gives", "given", "next", "with", "about", "how", "here", "called",
        "call", "doing", "do", "does", "where", "each", "other", "this", "after", "before", "be", "own", "more", "start",
        "my", "myself", "mine", "get", "gets", "up", "down", "meet", "met", "part", "parts", "ft"
    };

    private static readonly string[] AlwaysLowerCaseWords =
    {
        "etc"
    };

    private static readonly string[] Ordinals =
    {
        "th", "st", "rd", "s"
    };

    public static readonly IDictionary<string, Regex>
        Expressions = LesserWords.ToDictionary(x => x,
                x => new Regex($@"(?<!^'?""?){x}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .AddRange(AlwaysLowerCaseWords.ToDictionary(x => x,
                x => new Regex($@"\b{x}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)))
            .AddRange(Ordinals.ToDictionary(x => x,
                x => new Regex($@"(?<=\d'?){x}", RegexOptions.Compiled | RegexOptions.IgnoreCase)));
}