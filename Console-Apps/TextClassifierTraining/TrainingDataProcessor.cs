using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subreddit;
using TextClassifierTraining.Models;

namespace TextClassifierTraining;

public class TrainingDataProcessor(
    ISubredditPostProvider subredditPostProvider,
    ISubredditRepository subredditRepository,
    IPodcastRepository podcastRepository,
    ISubjectCleanser subjectCleanser,
    ISubjectService subjectService,
    ILogger<TrainingDataProcessor> logger)
{
    private const string TrainingDataLocation = "training-data";
    private const string ContainerName = "training-data";
    private const string DataSetName = "cultpodcasts";
    private const string ProjectName = "cultpodcasts";
    private const string LabelsFilename = "labels.json";


    public async Task Process(TrainingDataRequest request)
    {
        if (request.CreateLocalSubredditPostsRepository)
        {
            var posts = subredditPostProvider.GetPosts().Select(x => x.ToRedditPost());
            foreach (var post in posts)
            {
                await subredditRepository.Save(post);
            }
        }

        if (!Directory.Exists(TrainingDataLocation))
        {
            Directory.CreateDirectory(TrainingDataLocation);
        }

        var redditPosts = await subredditRepository.GetAll().ToArrayAsync();
        logger.LogInformation("Total reddit posts: {Count}", redditPosts.Count());
        logger.LogInformation(
            "Total reddit posts with links: {Count}", redditPosts.Count(x => !string.IsNullOrWhiteSpace(x.Url)));

        var redditPostMetaDatas =
            redditPosts
                .Where(x => !string.IsNullOrWhiteSpace(x.LinkFlairText))
                .Select(x => x.ToRedditPostMetaData())
                .Where(x => x != null)
                .Cast<RedditPostMetaData>();

        logger.LogInformation(
            "Total reddit posts with understood links: {Count}", redditPostMetaDatas.Count());

        logger.LogInformation(
            "Total reddit posts with flair: {Count}", redditPosts.Where(x => !string.IsNullOrWhiteSpace(x.LinkFlairText)).Count());


        var podcasts = await podcastRepository.GetAll().ToListAsync();

        var podcastEpisodes = podcasts.SelectMany(podcast => podcast.Episodes,
            (podcast, episode) => new PodcastEpisode(podcast, episode));

        var labels = new Labels
        {
            MetaData =
            {
                StorageInputContainerName = ContainerName,
                ProjectName = ProjectName
            }
        };

        foreach (var podcastEpisode in podcastEpisodes)
        {
            RedditPostMetaData? redditPostMetaData = null;
            if (podcastEpisode.Episode.AppleId != null)
            {
                var candidates = redditPostMetaDatas.Where(x =>
                    x.AppleId.HasValue && x.AppleId == podcastEpisode.Episode.AppleId);
                redditPostMetaData = candidates.FirstOrDefault();
            }

            if (redditPostMetaData == null && !string.IsNullOrWhiteSpace(podcastEpisode.Episode.SpotifyId))
            {
                var candidates = redditPostMetaDatas.Where(x =>
                    !string.IsNullOrWhiteSpace(x.SpotifyId) && x.SpotifyId == podcastEpisode.Episode.SpotifyId);
                redditPostMetaData = candidates.FirstOrDefault();
            }

            if (redditPostMetaData == null && !string.IsNullOrWhiteSpace(podcastEpisode.Episode.YouTubeId))
            {
                var candidates = redditPostMetaDatas.Where(x =>
                    !string.IsNullOrWhiteSpace(x.YouTubeId) && x.YouTubeId == podcastEpisode.Episode.YouTubeId);
                redditPostMetaData = candidates.FirstOrDefault();
            }

            var flair = redditPostMetaData?.Flair;
            var id = podcastEpisode.Episode.Id;
            var subjects = new List<string>();
            if (!string.IsNullOrWhiteSpace(flair) || podcastEpisode.Episode.Subjects.Any())
            {
                var podcastSubjects = podcastEpisode.Episode.Subjects;

                if (podcastSubjects.Any())
                {
                    subjects.AddRange(podcastSubjects);
                }

                if (!string.IsNullOrWhiteSpace(flair))
                {
                    subjects.Add(flair);
                }

                (var unmatched, subjects) = await subjectCleanser.CleanSubjects(subjects);
                if (unmatched)
                {
                    logger.LogError(
                        "Podcast '{PodcastName}' id:'{PodcastId}' Episode-id:'{EpisodeId}'.", podcastEpisode.Podcast.Name, podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                }
            }
            else
            {
                subjects = (await subjectService.Match(
                        podcastEpisode.Episode,
                        podcastEpisode.Podcast.IgnoredAssociatedSubjects,
                        podcastEpisode.Podcast.IgnoredSubjects))
                    .OrderByDescending(x => x.MatchResults.Sum(y => y.Matches)).Select(x => x.Subject.Name).ToList();
                if (!subjects.Any())
                {
                    logger.LogError(
                        "MISSING: '{EpisodeTitle}' - '{EpisodeDescription}'.", podcastEpisode.Episode.Title, podcastEpisode.Episode.Description);
                }
            }

            if (subjects.Any())
            {
                var filename = $"{id}.txt";
                await File.WriteAllTextAsync(Path.Combine(TrainingDataLocation, filename),
                    $@"{podcastEpisode.Episode.Title}
${podcastEpisode.Episode.Description}");

                foreach (var subject in subjects.Distinct().Select(x => x.ToLower()))
                {
                    if (!labels.Assets.Classes.Select(x => x.Category.ToLower()).Contains(subject))
                    {
                        labels.Assets.Classes.Add(new Class {Category = subject});
                    }
                }

                var documentSubjects = subjects.Distinct().Select(x => new Class {Category = x.ToLower()}).ToList();

                //if (documentSubjects.Count > 1)
                //{
                //    _logger.LogInformation(
                //        $"'{flairedEpisode.Item1.Name}' episode {flairedEpisode.Item2.Id} multiple-subjects: {string.Join(", ", subjects.Select(x => $"'{x}'"))}.");
                //}

                labels.Assets.Documents.Add(new Document
                {
                    Location = filename,
                    Class = documentSubjects.First()
                });
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var jsonString =
                JsonSerializer.Serialize(labels, jsonOptions);
            await File.WriteAllTextAsync(Path.Combine(TrainingDataLocation, LabelsFilename), jsonString);
        }
    }
}