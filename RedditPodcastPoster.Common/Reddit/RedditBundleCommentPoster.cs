using System.Text;
using Microsoft.Extensions.Logging;
using Reddit.Controllers;
using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Reddit;

public class RedditBundleCommentPoster : IRedditBundleCommentPoster
{
    private readonly ILogger<RedditBundleCommentPoster> _logger;

    public RedditBundleCommentPoster(ILogger<RedditBundleCommentPoster> logger)
    {
        _logger = logger;
    }

    public async Task Post(PostModel postModel, LinkPost result)
    {
        var comment = new StringBuilder();
        for (var i = 0; i < postModel.BundledPartNumbers.Count(); i++)
        {
            var episode = postModel.Episodes.Skip(i).First();
            if (!(episode.Spotify == null && episode.Apple == null))
            {
                comment.AppendLine(
                    $"**Part {postModel.BundledPartNumbers.Skip(i).First()}, {episode.Release}, {episode.Duration}**");
                comment.AppendLine();
            }

            if (episode.Spotify != null)
            {
                comment.AppendLine(
                    $"Spotify: {episode.Spotify.ToString()}");
                comment.AppendLine();
            }

            if (episode.Apple != null)
            {
                comment.AppendLine(
                    $"Apple Podcasts: {episode.Apple.ToString()}");
                comment.AppendLine();
            }
        }

        var commentContents = comment.ToString();
        if (!string.IsNullOrWhiteSpace(commentContents))
        {
            await result.ReplyAsync(comment.ToString());
        }
    }
}