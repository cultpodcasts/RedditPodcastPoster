using System.Text;
using Microsoft.Extensions.Logging;
using Reddit.Controllers;
using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Reddit;

public class RedditEpisodeCommentPoster : IRedditEpisodeCommentPoster
{
    private readonly ILogger<RedditEpisodeCommentPoster> _logger;

    public RedditEpisodeCommentPoster(ILogger<RedditEpisodeCommentPoster> logger)
    {
        _logger = logger;
    }

    public async Task Post(PostModel postModel, LinkPost result)
    {
        var body = new StringBuilder();
        if (postModel.HasYouTubeUrl && postModel.Spotify != null)
        {
            body.AppendLine($"Spotify: {postModel.Spotify.ToString()}");
            body.AppendLine();
        }

        if (postModel.Apple != null && !postModel.HasYouTubeUrl &&
            postModel.Spotify != null)
        {
            body.AppendLine($"Apple Podcasts: {postModel.Apple}");
        }

        var comment = body.ToString();
        if (!string.IsNullOrWhiteSpace(comment))
        {
            await result.ReplyAsync(body.ToString());
        }
    }
}