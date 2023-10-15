using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;

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
        PodcastEpisode? podcastEpisode = null;
        try
        {
            podcastEpisode = await GetPodcastEpisode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        if (podcastEpisode != null)
        {
            try
            {
                await PostTweet(podcastEpisode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"Failure to post-tweet for podcast with id '{podcastEpisode.Podcast.Id}' and episode-id '{podcastEpisode.Episode.Id}'.");
                throw;
            }
        }
    }

    private async Task<PodcastEpisode?> GetPodcastEpisode()
    {
        List<Podcast> podcasts;
        try
        {
            podcasts = await _repository.GetAll().ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure to retrieve podcasts");
            throw;
        }

        var podcastEpisode =
            podcasts
                .SelectMany(p => p.Episodes.Select(e => new {Podcast = p, Episode = e}))
                .Where(x =>
                    x.Episode.Release >= DateTime.UtcNow.Date.AddHours(-24) &&
                    x.Episode is {Removed: false, Ignored: false, Tweeted: false} &&
                    (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                    !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
                .MaxBy(x => x.Episode.Release);
        if (podcastEpisode?.Podcast == null)
        {
            _logger.LogInformation("No Podcast-Episode found to Tweet.");
            return null;
        }

        return new PodcastEpisode(podcastEpisode.Podcast, podcastEpisode.Episode);
    }

    private async Task PostTweet(PodcastEpisode podcastEpisode)
    {
        var tweet = BuildTweet(podcastEpisode);
        bool tweeted;
        try
        {
            tweeted = await _twitterClient.Send(tweet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to send tweet for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}', tweet: '{tweet}'.");
            throw;
        }

        if (tweeted)
        {
            podcastEpisode.Episode.Tweeted = true;
            try
            {
                await _repository.Update(podcastEpisode.Podcast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"Failure to save podcast with podcast-id '{podcastEpisode.Podcast.Id}' to update episode with id '{podcastEpisode.Episode.Id}'.");
                throw;
            }


            _logger.LogInformation($"Tweeted '{tweet}'.");
        }
        else
        {
            var message =
                $"Could not post tweet for podcast-episode: Podcast-id: '{podcastEpisode.Podcast.Id}', Episode-id: '{podcastEpisode.Episode.Id}'. Tweet: '{tweet}'.";
            _logger.LogError(message);
            throw new Exception(message);
        }
    }

    private string BuildTweet(PodcastEpisode podcastEpisode)
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
            $"{podcastEpisode.Episode.Release.ToString("d MMM yyyy")} {podcastEpisode.Episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture)}");
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
        return tweet;
    }
}