using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Subreddit;

namespace TextClassifierTraining;

public class TrainingDataProcessor
{
    private readonly ILogger<TrainingDataProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly ISubredditPostProvider _subredditPostProvider;
    private readonly ISubredditRepository _subredditRepository;

    public TrainingDataProcessor(
        ISubredditPostProvider subredditPostProvider,
        ISubredditRepository subredditRepository,
        IPodcastRepository podcastRepository,
        IRepositoryFactory repositoryFactory,
        ILogger<TrainingDataProcessor> logger)
    {
        _subredditPostProvider = subredditPostProvider;
        _subredditRepository = subredditRepository;
        _podcastRepository = podcastRepository;
        _repositoryFactory = repositoryFactory;
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

        var trainingDataRepository = _repositoryFactory.Create<TrainingData>("training-data");

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
                if (candidates.Count() > 1)
                {
                    var breakpoint = 1;
                }

                redditPostMetaData = candidates.FirstOrDefault();
            }

            if (redditPostMetaData == null && !string.IsNullOrWhiteSpace(podcastEpisode.Episode.SpotifyId))
            {
                var candidates = redditPostMetaDatas.Where(x =>
                    !string.IsNullOrWhiteSpace(x.SpotifyId) && x.SpotifyId == podcastEpisode.Episode.SpotifyId);
                if (candidates.Count() > 1)
                {
                    var breakpoint = 1;
                }

                redditPostMetaData = candidates.FirstOrDefault();
            }

            if (redditPostMetaData == null && !string.IsNullOrWhiteSpace(podcastEpisode.Episode.YouTubeId))
            {
                var candidates = redditPostMetaDatas.Where(x =>
                    !string.IsNullOrWhiteSpace(x.YouTubeId) && x.YouTubeId == podcastEpisode.Episode.YouTubeId);
                if (candidates.Count() > 1)
                {
                    var breakpoint = 1;
                }

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

            var trainingData = new TrainingData(
                id,
                flairedEpisode.Item2.Title,
                flairedEpisode.Item2.Description,
                subjects.Distinct().ToArray());
            await trainingDataRepository.Save(id.ToString(), trainingData);
        }
    }
}