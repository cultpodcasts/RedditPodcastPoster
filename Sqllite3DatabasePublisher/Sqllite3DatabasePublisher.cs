using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;

namespace Sqllite3DatabasePublisher;

public class Sqllite3DatabasePublisher
{
    private readonly ILogger<Sqllite3DatabasePublisher> _logger;
    private readonly PodcastContext _podcastContext;
    private readonly IPodcastRepository _podcastRepository;

    public Sqllite3DatabasePublisher(
        IPodcastRepository podcastRepository,
        PodcastContext podcastContext,
        ILogger<Sqllite3DatabasePublisher> logger)
    {
        _podcastRepository = podcastRepository;
        _podcastContext = podcastContext;
        _logger = logger;
    }

    public async Task Run()
    {
        await _podcastContext.Database.EnsureCreatedAsync();
        var podcasts = await _podcastRepository.GetAll().ToListAsync();

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
            _podcastContext.Podcasts.Add(entity);
            await _podcastContext.SaveChangesAsync();
        }

        var query = from b in _podcastContext.Podcasts
            select b;

        Console.WriteLine("All podcasts in the database:");
        foreach (var item in query)
        {
            Console.WriteLine(item.Guid);
        }
    }
}