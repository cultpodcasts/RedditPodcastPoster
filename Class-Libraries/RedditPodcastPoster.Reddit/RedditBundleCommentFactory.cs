using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class RedditBundleCommentFactory(ILogger<RedditBundleCommentFactory> logger) : IRedditBundleCommentFactory
{
    public string Post(PostModel postModel)
    {
        var comment = new StringBuilder();
        for (var i = 0; i < postModel.BundledPartNumbers.Count(); i++)
        {
            var episode = postModel.Episodes.Skip(i).First();
            if ((episode.Spotify != null && episode.Apple != null) || episode.YouTube != null)
            {
                comment.AppendLine(
                    $"**Part {postModel.BundledPartNumbers.Skip(i).First()}, {episode.Release} {episode.Duration}**");
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

            if (episode.YouTube != null)
            {
                comment.AppendLine(
                    $"YouTube: {episode.YouTube.ToString()}");
                comment.AppendLine();
            }
        }

        return comment.ToString();
    }
}