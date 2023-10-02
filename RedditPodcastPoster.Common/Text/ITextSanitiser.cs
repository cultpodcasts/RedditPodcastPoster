using System.Text.RegularExpressions;
using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Text;

public interface ITextSanitiser
{
    string ExtractTitle(string input, Regex regex);
    string ExtractBody(string body, Regex regex);
    string SanitiseTitle(PostModel postModel);
    string SanitisePodcastName(PostModel postModel);
    string SanitiseDescription(PostModel postModel);
}