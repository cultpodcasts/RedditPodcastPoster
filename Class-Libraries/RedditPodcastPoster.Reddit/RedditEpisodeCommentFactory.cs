using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class RedditEpisodeCommentFactory(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<RedditEpisodeCommentFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IRedditEpisodeCommentFactory
{
    public string ToComment(PostModel postModel)
    {
        var body = new StringBuilder();
        var links = new Dictionary<string, (string Prefix, Uri? Url)>
        {
            {"YouTube", ("\ud83d\udfe5 ", postModel.YouTube)},
            {"Spotify", ("\ud83d\udfe2 ", postModel.Spotify)},
            {"Apple Podcasts", ("\ud83d\udfe3 ", postModel.Apple)},
            {"Internet Archive", (string.Empty, postModel.InternetArchive)},
            {"BBC", (string.Empty, postModel.BBC)}
        };
        var availableKeys = links
            .Where(x => x.Value.Url != null)
            .Select(x => x.Key);

        if (availableKeys.Count() > 1)
        {
            body.AppendLine("Links:");
            body.AppendLine("");

            foreach (var availableKey in availableKeys)
            {
                body.AppendLine($"{links[availableKey].Prefix}[{availableKey}]({links[availableKey].Url})");
                body.AppendLine("");
            }
        }

        return body.ToString();
    }
}