using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.TitleCasing;

public interface ITitleCasingRulesProvider
{
    IReadOnlyDictionary<string, LanguageTitleCasingRulesDocument> GetAll();

    IDictionary<string, Regex> GetLowerCaseExpressions(string? language);

    IReadOnlyList<KnownTermEntry> GetKnownTerms(string? language);
}
