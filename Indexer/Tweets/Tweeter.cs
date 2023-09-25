using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.Text;

namespace Indexer.Tweets;

public class Tweeter : ITweeter
{
    private readonly ILogger<Tweeter> _logger;
    private readonly IPodcastRepository _repository;
    private readonly TextInfo _textInfo = new CultureInfo("en-GB", false).TextInfo;
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
        if (podcastEpisode != null)
        {
            var episodeTitle = podcastEpisode.Episode.Title;
            if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.TitleRegex))
            {
                episodeTitle =
                    _textSanitiser.ExtractTitle(episodeTitle, new Regex(podcastEpisode.Podcast.TitleRegex));
            }

            episodeTitle = _textSanitiser.FixCharacters(episodeTitle);
            episodeTitle = _textInfo.ToTitleCase(episodeTitle.ToLower());

            var podcastName = _textSanitiser.FixCharacters(podcastEpisode.Podcast.Name);
            podcastName = _textInfo.ToTitleCase(podcastName.ToLower());

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
}