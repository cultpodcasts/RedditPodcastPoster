using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Common.Text;

public interface ITextSanitiser
{
    string Sanitise(string text);
    string ExtractTitle(string input, Regex regex);
    string ExtractBody(string body, Regex regex);
    string FixCharacters(string title);
    string FixCasing(string input);
}