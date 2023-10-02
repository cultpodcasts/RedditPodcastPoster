using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.Text;

namespace Indexer.Tweets;

public class Tweeter : ITweeter
{
    private readonly ILogger<Tweeter> _logger;
    private readonly IPodcastRepository _repository;
    private readonly ITextSanitiser _textSanitiser;
    private readonly ITwitterClient _twitterClient;

    public Tweeter(
        ITwitterClient twitterClient,
        IPodcastRepository repository,
        ITextSanitiser textSanitiser,
        ILogger<Tweeter> logger)
    {
        _twitterClient = twitterClient;
        _repository = repository;
        _textSanitiser = textSanitiser;
        _logger = logger;
    }

    public async Task Tweet()
    {
        var podcastEpisode = await GetPodcastEpisode();
        if (podcastEpisode != null)
        {
            await PostTweet(podcastEpisode);
        }
    }

    private async Task<PodcastEpisode?> GetPodcastEpisode()
    {
        var podcasts = await _repository.GetAll().ToListAsync();
        var podcastEpisodes =
            podcasts
                .Where(p => p.IndexAllEpisodes)
                .SelectMany(p =>
                    p.Episodes.Select(e => new {Podcast = p, Episode = e}));
        var podcastEpisode = podcastEpisodes
            .Where(x =>
                x.Episode.Release >= DateTime.UtcNow.Date &&
                x.Episode is {Removed: false, Ignored: false, Tweeted: false} &&
                (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null || x.Episode.Urls.Apple != null))
            .MinBy(x => x.Episode.Release);
        if (podcastEpisode?.Podcast==null)
        {
            _logger.LogInformation($"No Podcast-Episode found to Tweet.");
            return null;
        }

        return new PodcastEpisode(podcastEpisode.Podcast, podcastEpisode.Episode);
    }

    private async Task PostTweet(PodcastEpisode podcastEpisode)
    {
        var postModel = (podcastEpisode.Podcast, new[] {podcastEpisode.Episode}).ToPostModel();
        var episodeTitle = _textSanitiser.SanitiseTitle(postModel);
        var podcastName = _textSanitiser.SanitisePodcastName(postModel);

        var tweetBuilder = new StringBuilder();
        tweetBuilder.AppendLine($"\"{episodeTitle}\"");
        if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.TwitterHandle))
        {
            tweetBuilder.AppendLine($"{podcastName} {podcastEpisode.Podcast.TwitterHandle}");
        }
        else
        {
            tweetBuilder.AppendLine($"{podcastName}");
        }

        tweetBuilder.AppendLine(
            $"{podcastEpisode.Episode.Release.ToString("dd MMM yyyy")} {podcastEpisode.Episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture)}");
        tweetBuilder.AppendLine("#CultPodcasts");
        if (podcastEpisode.Episode.Urls.YouTube != null)
        {
            tweetBuilder.AppendLine(podcastEpisode.Episode.Urls.YouTube.ToString());
        }
        else if (podcastEpisode.Episode.Urls.Spotify != null)
        {
            tweetBuilder.AppendLine(podcastEpisode.Episode.Urls.Spotify.ToString());
        }
        else
        {
            tweetBuilder.AppendLine(podcastEpisode.Episode.Urls.Apple!.ToString());
        }

        var tweet = tweetBuilder.ToString();
        var tweeted = await _twitterClient.Send(tweet);
        if (tweeted)
        {
            podcastEpisode.Episode.Tweeted = true;
            await _repository.Update(podcastEpisode.Podcast);
            _logger.LogInformation($"Tweeted '{tweet}'.");
        }
        else
        {
            var message =
                $"Could not post tweet for candidate-podcast-episode: Podcast-id: '{podcastEpisode.Podcast.Id}', Episode-id: '{podcastEpisode.Episode.Id}'. Tweet: '{tweet}'.";
            _logger.LogError(message);
            throw new Exception(message);
        }
    }
}