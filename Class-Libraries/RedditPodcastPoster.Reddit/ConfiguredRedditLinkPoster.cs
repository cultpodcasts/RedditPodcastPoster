using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class ConfiguredRedditLinkPoster(
    RedditLinkPoster redditLinkPoster,
    DevvitRedditLinkPoster devvitRedditLinkPoster,
    IOptions<SubredditSettings> subredditSettings) : IRedditLinkPoster
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public Task<PostResponse> Post(PostModel postModel)
    {
        if (_subredditSettings.UseDevvit)
        {
            return devvitRedditLinkPoster.Post(postModel);
        }

        return redditLinkPoster.Post(postModel);
    }
}
