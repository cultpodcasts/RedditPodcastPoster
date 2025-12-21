using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.ContentPublisher;

public class QueryExecutor(
    Container container,
    ITextSanitiser textSanitiser,
    ISubjectRepository subjectRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<QueryExecutor> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IQueryExecutor
{
    private readonly IEnumerable<Subject> subjects = subjectRepository.GetAll().ToBlockingEnumerable();

    public async Task<HomePageModel> GetHomePage(CancellationToken ct)
    {
        var podcastResults = GetRecentPodcasts(container, ct);

        var count = GetEpisodeCount(container, ct);

        var totalDuration = GetTotalDuration(container, ct);

        IEnumerable<Task> tasks = [podcastResults, count, totalDuration];

        await Task.WhenAll(tasks);

        return new HomePageModel
        {
            EpisodeCount = count.Result,
            RecentEpisodes = podcastResults.Result
                .OrderByDescending(x => x.Release)
                .Select(Santitise)
                .Select(x =>
                    new RecentEpisode
                    {
                        EpisodeId = x.EpisodeId,
                        EpisodeDescription = WebUtility.HtmlDecode(x.EpisodeDescription),
                        EpisodeTitle = WebUtility.HtmlDecode(x.EpisodeTitle),
                        PodcastName = x.PodcastName,
                        Release = x.Release,
                        Spotify = x.Spotify,
                        Apple = x.Apple,
                        YouTube = x.YouTube,
                        BBC = x.BBC,
                        InternetArchive = x.InternetArchive,
                        Length = TimeSpan.FromSeconds(Math.Round(x.Length.TotalSeconds)),
                        Subjects = x.Subjects != null && x.Subjects.Any() ? x.Subjects : null,
                        Image = x.Images?.YouTube ?? x.Images?.Spotify ?? x.Images?.Apple ?? x.Images?.Other
                    }),
            TotalDuration = totalDuration.Result
        };
    }

    public SubjectModel GetSubjects()
    {
        var termSubjects = new Dictionary<string, List<string>>();
        foreach (var subject in subjects)
        {
            AddTerm(termSubjects, subject.Name, subject.Name);
            if (subject.Aliases != null)
            {
                foreach (var term in subject.Aliases)
                {
                    AddTerm(termSubjects, term, subject.Name);
                }
            }

            if (subject.AssociatedSubjects != null)
            {
                foreach (var term in subject.AssociatedSubjects)
                {
                    AddTerm(termSubjects, term, subject.Name);
                }
            }
        }

        var subjectModel = new SubjectModel
        {
            TermSubjects = termSubjects
                .ToDictionary(
                    x => x.Key,
                    x => x.Value.DistinctBy(y => y.ToLowerInvariant()).ToArray())
        };
        return subjectModel;
    }

    private void AddTerm(Dictionary<string, List<string>> dict, string term, string subject)
    {
        if (!dict.ContainsKey(term))
        {
            dict[term] = new List<string>();
        }

        dict[term].Add(subject);
    }

    private static async Task<int> GetEpisodeCount(Container c, CancellationToken ct)
    {
        var numberOfEpisodes = new QueryDefinition(@"
                SELECT Count(p.episodes)
                FROM
                  podcasts p
                JOIN
                e IN p.episodes
                WHERE ((NOT IS_DEFINED(p.removed)) OR p.removed=false)
                AND   e.removed=false
                ");
        using var episodeCount = c.GetItemQueryIterator<ScalarResult<int>>(
            numberOfEpisodes
        );
        var item = await episodeCount.ReadNextAsync(ct);
        var count = item.First().Item;

        return count;
    }

    private static async Task<TimeSpan> GetTotalDuration(Container c, CancellationToken ct)
    {
        var durationResults = new List<DurationResult>();

        var allDurations = new QueryDefinition(@"
               SELECT e.duration
               FROM
                 podcasts p
               JOIN
               e IN p.episodes
               WHERE ((NOT IS_DEFINED(p.removed)) OR p.removed=false)
               AND   e.ignored=false AND e.removed=false
        ");
        using var feed = c.GetItemQueryIterator<DurationResult>(allDurations);
        while (feed.HasMoreResults)
        {
            durationResults.AddRange(await feed.ReadNextAsync(ct));
        }

        return TimeSpan.FromTicks(durationResults.Sum(x => x.Duration.Ticks));
    }

    private static async Task<List<PodcastResult>> GetRecentPodcasts(Container c, CancellationToken ct)
    {
        var podcastResults = new List<PodcastResult>();
        var lastWeeksEpisodes = new QueryDefinition(
            @"
                           SELECT
                           p.name as podcastName,
                           p.titleRegex as titleRegex,
                           p.descriptionRegex as descriptionRegex,
                           e.id as episodeId,
                           e.title as episodeTitle,
                           e.description as episodeDescription,
                           e.release as release,
                           e.urls.spotify as spotify,
                           e.urls.apple as apple,
                           e.urls.youtube as youtube,
                           e.urls.bbc as bbc,
                           e.urls.internetArchive as internetArchive,
                           e.duration as length,
                           e.subjects as subjects,
                           e.images as images,
                           p.knownTerms as knownTerms
                           FROM
                           podcasts p
                           JOIN
                           e IN p.episodes
                           WHERE ((NOT IS_DEFINED(p.removed)) OR p.removed=false)
                           AND e.removed=false
                           AND e.ignored=false
                           AND DateTimeDiff('dd', e.release, GetCurrentDateTime()) < 7
            ");

        using var feed = c.GetItemQueryIterator<PodcastResult>(lastWeeksEpisodes);
        while (feed.HasMoreResults)
        {
            var readNextAsync = await feed.ReadNextAsync(ct);
            podcastResults.AddRange(readNextAsync);
        }

        return podcastResults;
    }

    private PodcastResult Santitise(PodcastResult podcastResult)
    {
        Regex? titleRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.TitleRegex))
        {
            titleRegex = new Regex(podcastResult.TitleRegex);
        }

        var subjectKnownTerms = (podcastResult.Subjects ?? [])
            .Select(x => subjects.SingleOrDefault(y => y.Name == x))
            .SelectMany(x => x?.KnownTerms ?? []).ToArray();

        podcastResult.EpisodeTitle = textSanitiser.SanitiseTitle(podcastResult.EpisodeTitle, titleRegex,
            podcastResult.KnownTerms, subjectKnownTerms);

        Regex? descRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.DescriptionRegex))
        {
            descRegex = new Regex(podcastResult.DescriptionRegex, RegexOptions.Singleline);
        }

        var desc =
            textSanitiser.SanitiseDescription(podcastResult.EpisodeDescription, descRegex);
        podcastResult.EpisodeDescription = desc;
        //podcastResult.PodcastName = textSanitiser.SanitisePodcastName(podcastResult.PodcastName);

        return podcastResult;
    }
}