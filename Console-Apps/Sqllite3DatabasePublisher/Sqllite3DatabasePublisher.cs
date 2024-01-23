using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Sqllite3DatabasePublisher;

public class Sqllite3DatabasePublisher(
    IPodcastRepository podcastRepository,
    PodcastContext podcastContext,
    ILogger<Sqllite3DatabasePublisher> logger)
{
    public async Task Run()
    {
        await podcastContext.Database.EnsureCreatedAsync();
        var podcasts = await podcastRepository.GetAll().ToListAsync();

        foreach (var podcast in podcasts)
        {
            var entity = new Podcast
            {
                Guid = podcast.Id,
                PrimaryPostService = podcast.PrimaryPostService,
                Name = podcast.Name,
                Publisher = podcast.Publisher,
                Episodes = podcast.Episodes
                    .Where(episode => !episode.Removed)
                    .Select(episode => new Episode
                    {
                        Guid = episode.Id,
                        Release = episode.Release,
                        Length = episode.Length,
                        Explicit = episode.Explicit,
                        Title = episode.Title,
                        Description = episode.Description,
                        Subjects = string.Join(",", episode.Subjects),
                        YouTube = episode.Urls.YouTube,
                        Spotify = episode.Urls.Spotify,
                        Apple = episode.Urls.Apple
                    }).ToList()
            };
            podcastContext.Podcasts.Add(entity);
            await podcastContext.SaveChangesAsync();
        }

        var query = from b in podcastContext.Podcasts
            select b;

        Console.WriteLine("All podcasts in the database:");
        foreach (var item in query)
        {
            Console.WriteLine(item.Guid);
        }
    }
}