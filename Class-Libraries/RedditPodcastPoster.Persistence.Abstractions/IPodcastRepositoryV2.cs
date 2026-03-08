using System.Linq.Expressions;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPodcastRepositoryV2
{
    Task<Podcast?> GetPodcast(Guid podcastId);
    Task Save(Podcast podcast);
    IAsyncEnumerable<Podcast> GetAll();
    Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector);
    IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector);
}
