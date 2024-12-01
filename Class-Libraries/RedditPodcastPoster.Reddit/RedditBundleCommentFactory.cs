using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class RedditBundleCommentFactory(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<RedditBundleCommentFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IRedditBundleCommentFactory
{
    public string ToComment(PostModel postModel)
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
                comment.AppendLine($"\ud83d\udfe2 [Spotify]({episode.Spotify.ToString()})");
                comment.AppendLine();
            }

            if (episode.Apple != null)
            {
                comment.AppendLine($"\ud83d\udfe3 [Apple Podcasts]({episode.Apple.ToString()})");
                comment.AppendLine();
            }

            if (episode.YouTube != null)
            {
                comment.AppendLine($"\ud83d\udfe5 [YouTube]({episode.YouTube.ToString()})");
                comment.AppendLine();
            }

            if (episode.InternetArchive != null)
            {
                comment.AppendLine($"[Internet Archive]({episode.InternetArchive.ToString()})");
                comment.AppendLine();
            }

            if (episode.BBC != null)
            {
                comment.AppendLine($"[BBC]({episode.BBC.ToString()})");
                comment.AppendLine();
            }
        }

        return comment.ToString();
    }
}