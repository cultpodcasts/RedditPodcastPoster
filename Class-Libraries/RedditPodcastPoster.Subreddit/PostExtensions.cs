using Reddit.Controllers;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subreddit;

public static class PostExtensions {
    public static RedditPost ToRedditPost(this Post post)
    {
        return new RedditPost(Guid.NewGuid())
        {
            FullName = post.Fullname,
            Author = post.Author,
            RedditId = post.Id,
            Created = post.Created,
            Edited = post.Edited,
            Removed = post.Removed,
            Spam = post.Spam,
            NSFW = post.NSFW,
            UpVotes = post.UpVotes,
            UpVoteRatio = post.UpvoteRatio,
            Title = post.Title,
            DownsVotes = post.DownVotes,
            LinkFlairText = post.Listing.LinkFlairText,
            Url = post.Listing.URL,
            IsVideo = post.Listing.IsVideo,
            Text = post.Listing.SelfText,
            Html = post.Listing.SelfTextHTML
        };
    }
}