using Reddit.Controllers;

namespace RedditPodcastPoster.Reddit.Models;

public record PostResponse(LinkPost? LinkPost, bool Posted);