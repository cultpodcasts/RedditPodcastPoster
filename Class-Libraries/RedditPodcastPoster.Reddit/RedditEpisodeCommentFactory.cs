using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class RedditEpisodeCommentFactory : IRedditEpisodeCommentFactory
{
    private readonly ILogger<RedditEpisodeCommentFactory> _logger;

    public RedditEpisodeCommentFactory(ILogger<RedditEpisodeCommentFactory> logger)
    {
        _logger = logger;
    }

    public string Post(PostModel postModel)
    {
        var body = new StringBuilder();

        var links = new Dictionary<string, Uri?>
        {
            {"YouTube", postModel.YouTube},
            {"Spotify", postModel.Spotify},
            {"Apple Podcasts", postModel.Apple}
        };
        var availableKeys = links.Where(x => x.Value != null && x.Value != postModel.Link).Select(x => x.Key);
        foreach (var availableKey in availableKeys)
        {
            body.AppendLine($"{availableKey}: {links[availableKey]}");
            body.AppendLine("");
        }

        return body.ToString();
    }
}