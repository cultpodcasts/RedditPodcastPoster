using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Common.Text;

public interface ITextSanitiser
{
    string Sanitise(string text);
    string ExtractTitle(string episodeTitle, Regex regex);
    string ExtractBody(string body, Regex regex);
    string FixCharacters(string title);
}