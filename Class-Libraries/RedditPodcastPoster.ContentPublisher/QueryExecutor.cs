﻿using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.ContentPublisher;

public class QueryExecutor : IQueryExecutor
{
    private readonly Container _container;
    private readonly ILogger<QueryExecutor> _logger;
    private readonly IRepository<Subject> _subjectRepository;
    private readonly ITextSanitiser _textSanitiser;

    public QueryExecutor(
        Container container,
        ITextSanitiser textSanitiser,
        IRepository<Subject> subjectRepository,
        ILogger<QueryExecutor> logger)
    {
        _container = container;
        _textSanitiser = textSanitiser;
        _subjectRepository = subjectRepository;
        _logger = logger;
    }

    public async Task<HomePageModel> GetHomePage(CancellationToken ct)
    {
        var podcastResults = GetRecentPodcasts(_container, ct);

        var count = GetEpisodeCount(_container, ct);

        var totalDuration = GetTotalDuration(_container, ct);

        IEnumerable<Task> tasks = new Task[] {podcastResults, count, totalDuration};

        await Task.WhenAll(tasks);

        return new HomePageModel
        {
            EpisodeCount = count.Result,
            RecentEpisodes = podcastResults.Result.OrderByDescending(x => x.Release).Select(Santitise).Select(x =>
                new RecentEpisode
                {
                    Apple = x.Apple,
                    EpisodeDescription = x.EpisodeDescription,
                    EpisodeTitle = x.EpisodeTitle,
                    PodcastName = x.PodcastName,
                    Release = x.Release,
                    Spotify = x.Spotify,
                    YouTube = x.YouTube,
                    Length = TimeSpan.FromSeconds(Math.Round(x.Length.TotalSeconds)),
                    Subjects = x.Subjects != null && x.Subjects.Any() ? x.Subjects : null
                }),
            TotalDuration = totalDuration.Result
        };
    }

    public async Task<SubjectModel> GetSubjects(CancellationToken ct)
    {
        var termSubjects = new Dictionary<string, List<string>>();
        var subjects = await _subjectRepository.GetAll(Subject.PartitionKey);
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

    private static async Task<int?> GetEpisodeCount(Container c, CancellationToken ct)
    {
        var numberOfEpisodes = new QueryDefinition(@"
                SELECT Count(p.episodes)
                FROM
                  podcasts p
                JOIN
                e IN p.episodes
                WHERE e.ignored=false AND e.removed=false
                ");
        using var episodeCount = c.GetItemQueryIterator<ScalarResult<int>>(
            numberOfEpisodes
        );
        int? count = null;
        if (episodeCount.HasMoreResults)
        {
            var item = await episodeCount.ReadNextAsync(ct);
            count = item.First().Item;
        }

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
               WHERE e.ignored=false AND e.removed=false
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
                           e.title as episodeTitle,
                           e.description as episodeDescription,
                           e.release as release,
                           e.urls.spotify as spotify,
                           e.urls.apple as apple,
                           e.urls.youtube as youtube,
                           e.duration as length,
                           e.subjects as subjects
                           FROM
                           podcasts p
                           JOIN
                           e IN p.episodes
                           WHERE e.removed=false
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

        podcastResult.EpisodeTitle = _textSanitiser.SanitiseTitle(podcastResult.EpisodeTitle, titleRegex);

        Regex? descRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.DescriptionRegex))
        {
            descRegex = new Regex(podcastResult.DescriptionRegex);
        }

        podcastResult.EpisodeDescription =
            _textSanitiser.SanitiseDescription(podcastResult.EpisodeDescription, descRegex);

        podcastResult.PodcastName = _textSanitiser.SanitisePodcastName(podcastResult.PodcastName);

        return podcastResult;
    }
}