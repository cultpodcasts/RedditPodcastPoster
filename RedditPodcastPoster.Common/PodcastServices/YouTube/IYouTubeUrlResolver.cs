using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeUrlResolver
{
    Task<Uri?> Resolve(Podcast podcast, Episode episode, DateTime? publishedSince);
    Uri GetYouTubeUrl(SearchResult matchedYouTubeVideo);
}