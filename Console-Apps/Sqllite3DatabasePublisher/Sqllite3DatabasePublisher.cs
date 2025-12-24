using System.Text.Json;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Sqllite3DatabasePublisher;

public class Sqllite3DatabasePublisher(
    IPodcastRepository podcastRepository,
    ISubjectRepository subjectRepository,
    DatabaseContext databaseContext,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<Sqllite3DatabasePublisher> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run(Request request)
    {
        await databaseContext.Database.EnsureCreatedAsync();
        await PopulatePodcasts();
        await PopulateSubjects();
        await databaseContext.SaveChangesAsync();


        var query = from b in databaseContext.Podcasts
            select b;

        Console.WriteLine("All podcasts in the database:");
        foreach (var item in query)
        {
            Console.WriteLine(item.Guid);
        }
    }

    private async Task PopulatePodcasts()
    {
        var podcasts = await podcastRepository
            .GetAllBy(
                podcast =>
                    ((!podcast.Removed.IsDefined() || podcast.Removed == false) &&
                     podcast.IndexAllEpisodes) || podcast.EpisodeIncludeTitleRegex != "",
                podcast => new { id = podcast.Id, name = podcast.Name })
            .ToListAsync();

        foreach (var podcast in podcasts)
        {
            logger.LogInformation(JsonSerializer.Serialize(podcast));

            var entity = new Podcast
            {
                Guid = podcast.id,
                Name = podcast.name
            };
            databaseContext.Podcasts.Add(entity);
        }
    }

    private async Task PopulateSubjects()
    {
        var subjects = await subjectRepository.GetAll().ToListAsync();

        foreach (var subject in subjects)
        {
            logger.LogInformation(JsonSerializer.Serialize(subject));

            var entity = new Subject
            {
                Guid = subject.Id,
                Name = subject.Name
            };
            databaseContext.Subjects.Add(entity);
        }
    }
}