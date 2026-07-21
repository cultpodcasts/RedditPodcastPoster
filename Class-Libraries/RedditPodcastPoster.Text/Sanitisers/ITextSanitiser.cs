using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Text.Sanitisers;

public interface ITextSanitiser
{
    Task<string> SanitiseTitle(PostModel postModel);
    string SanitisePodcastName(PostModel postModel);
    string SanitiseDescription(PostModel postModel);
    Task<string> SanitiseTitle(string episodeTitle, Regex? regex, string[] podcastKnownTerms, string[] subjectKnownTerms);
    string SanitisePodcastName(string podcastName);
    string SanitiseDescription(string episodeDescription, Regex? descriptionRegex);
    string ExtractDescription(string episodeDescription, Regex? descriptionRegex);
    string ExtractDescription(string episodeDescription, string descriptionRegex);
}
