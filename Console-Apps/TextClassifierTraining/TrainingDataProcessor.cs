using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Subreddit;
using TextClassifierTraining.Models;

namespace TextClassifierTraining;

public class TrainingDataProcessor
{
    private const string TrainingDataLocation = "training-data";
    private const string ContainerName = "training-data";
    private const string DataSetName = "cultpodcasts";
    private const string ProjectName = "cultpodcasts";
    private const string LabelsFilename = "labels.json";


    private readonly ILogger<TrainingDataProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly ISubjectCleanser _subjectCleanser;
    private readonly ISubredditPostProvider _subredditPostProvider;
    private readonly ISubredditRepository _subredditRepository;

    public TrainingDataProcessor(
        ISubredditPostProvider subredditPostProvider,
        ISubredditRepository subredditRepository,
        IPodcastRepository podcastRepository,
        ISubjectCleanser subjectCleanser,
        ILogger<TrainingDataProcessor> logger)
    {
        _subredditPostProvider = subredditPostProvider;
        _subredditRepository = subredditRepository;
        _podcastRepository = podcastRepository;
        _subjectCleanser = subjectCleanser;
        _logger = logger;
    }

    public async Task Process(TrainingDataRequest request)
    {
        if (request.CreateLocalSubredditPostsRepository)
        {
            var posts = _subredditPostProvider.GetPosts().Select(x => x.ToRedditPost());
            foreach (var post in posts)
            {
                await _subredditRepository.Save(post);
            }
        }

        if (!Directory.Exists(TrainingDataLocation))
        {
            Directory.CreateDirectory(TrainingDataLocation);
        }

        var redditPosts = await _subredditRepository.GetAll();
        _logger.LogInformation($"Total reddit posts: {redditPosts.Count()}");
        _logger.LogInformation(
            $"Total reddit posts with links: {redditPosts.Where(x => !string.IsNullOrWhiteSpace(x.Url)).Count()}");

        var redditPostMetaDatas =
            redditPosts
                .Where(x => !string.IsNullOrWhiteSpace(x.LinkFlairText))
                .Select(x => x.ToRedditPostMetaData())
                .Where(x => x != null)
                .Cast<RedditPostMetaData>();

        _logger.LogInformation(
            $"Total reddit posts with understood links: {redditPostMetaDatas.Count()}");

        _logger.LogInformation(
            $"Total reddit posts with flair: {redditPosts.Where(x => !string.IsNullOrWhiteSpace(x.LinkFlairText)).Count()}");


        var podcasts = await _podcastRepository.GetAll().ToListAsync();

        var podcastEpisodes = podcasts.SelectMany(podcast => podcast.Episodes,
            (podcast, episode) => new {Podcast = podcast, Episode = episode});

        var flairedEpisodes = new List<(Podcast, Episode, string?)>();
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

            flairedEpisodes.Add(new ValueTuple<Podcast, Episode, string?>(podcastEpisode.Podcast,
                podcastEpisode.Episode, redditPostMetaData?.Flair));
        }

        _logger.LogInformation($"Podcast Episodes: {flairedEpisodes.Count}");
        var flairedEpisodesWithFlair =
            flairedEpisodes.Where(x => !string.IsNullOrWhiteSpace(x.Item3) || x.Item2.Subjects.Any());
        _logger.LogInformation(
            $"Podcast Episodes with flair: {flairedEpisodesWithFlair.Count()}");

        var labels = new Labels();
        labels.MetaData.StorageInputContainerName = ContainerName;
        labels.MetaData.ProjectName = ProjectName;
        foreach (var flairedEpisode in flairedEpisodesWithFlair)
        {
            var id = flairedEpisode.Item2.Id;

            var podcastSubjects = flairedEpisode.Item2.Subjects;
            var flair = flairedEpisode.Item3;

            var subjects = new List<string>();
            if (podcastSubjects.Any())
            {
                subjects.AddRange(podcastSubjects);
            }

            if (!string.IsNullOrWhiteSpace(flair))
            {
                subjects.Add(flair);
            }

            subjects = await _subjectCleanser.CleanSubjects(subjects);

            if (subjects.Any())
            {
                var filename = $"{id}.txt";
                await File.WriteAllTextAsync(Path.Combine(TrainingDataLocation, filename),
                    $@"{flairedEpisode.Item2.Title}
${flairedEpisode.Item2.Description}");

                foreach (var subject in subjects.Distinct().Select(x => x.ToLower()))
                {
                    if (!labels.Assets.Classes.Select(x => x.Category.ToLower()).Contains(subject))
                    {
                        labels.Assets.Classes.Add(new Class {Category = subject});
                    }
                }

                var documentSubjects = subjects.Distinct().Select(x => new Class {Category = x.ToLower()}).ToList();

                if (documentSubjects.Count > 1)
                {
                    _logger.LogInformation(
                        $"'{flairedEpisode.Item1.Name}' episode {flairedEpisode.Item2.Id} multiple-subjects: {string.Join(", ", subjects.Select(x => $"'{x}'"))}.");
                }

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