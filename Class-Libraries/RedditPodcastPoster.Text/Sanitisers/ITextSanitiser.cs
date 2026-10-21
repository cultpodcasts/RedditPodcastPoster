using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Text.Sanitisers;

public interface ITextSanitiser
{
    Task<string> SanitiseTitle(PostModel postModel);
    string SanitisePodcastName(PostModel postModel);
    string SanitiseDescription(PostModel postModel);
    Task<string> SanitiseTitle(string episodeTitle, Regex? regex, string[] podcastKnownTerms, string[] subjectKnownTerms,
        string? language = null);
    string SanitisePodcastName(string podcastName);
    string SanitiseDescription(string episodeDescription, Regex? descriptionRegex);
    string ExtractDescription(string episodeDescription, Regex? descriptionRegex);

    /// <summary>
    /// Applies a podcast's description regex (pattern text) to an episode description, expanding the
    /// <c>{episode-title}</c> and <c>{podcast-name}</c> tokens via <c>DescriptionRegexTokens.Create</c>.
    /// A blank pattern returns <paramref name="episodeDescription"/> unchanged.
    /// </summary>
    string ExtractDescription(string episodeDescription, string descriptionRegex, string? episodeTitle,
        string? podcastName);
}
