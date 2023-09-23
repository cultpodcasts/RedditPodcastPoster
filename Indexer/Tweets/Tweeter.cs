using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.Text;
using Tweetinvi;

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
        _logger.LogInformation($"{nameof(Tweet)} initiated.");
        var podcasts = await _repository.GetAll().ToListAsync();
        var candidate =
            podcasts
                .Where(p => p.IndexAllEpisodes)
                .SelectMany(p => p.Episodes.Select(e => new {Podcast = p, Episode = e}))
                .Where(x => x.Episode.Release > DateTime.UtcNow.Date &&
                            x.Episode is {Removed: false, Ignored: false, Tweeted: false})
                .MinBy(x => x.Episode.Release);
        if (candidate != null)
        {
            var episodeTitle = candidate.Episode.Title;
            if (!string.IsNullOrWhiteSpace(candidate.Podcast.TitleRegex))
            {
                episodeTitle =
                    _textSanitiser.ExtractTitle(episodeTitle, new Regex(candidate.Podcast.TitleRegex));
            }

            episodeTitle = _textSanitiser.FixCharacters(episodeTitle);
            episodeTitle = _textInfo.ToTitleCase(episodeTitle.ToLower());

            var podcastName = _textSanitiser.FixCharacters(candidate.Podcast.Name);
            podcastName = _textInfo.ToTitleCase(podcastName.ToLower());

            var tweetBuilder = new StringBuilder();

            tweetBuilder.AppendLine(episodeTitle);
            tweetBuilder.AppendLine(podcastName);
            tweetBuilder.AppendLine(candidate.Episode.Release.ToString("dd MMM yyyy"));
            tweetBuilder.AppendLine(candidate.Episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture));
            if (candidate.Episode.Urls.YouTube != null)
            {
                tweetBuilder.AppendLine(candidate.Episode.Urls.YouTube.ToString());
            }
            else if (candidate.Episode.Urls.Spotify != null)
            {
                tweetBuilder.AppendLine(candidate.Episode.Urls.Spotify.ToString());
            }
            else
            {
                tweetBuilder.AppendLine(candidate.Episode.Urls.Apple.ToString());
            }

            var tweet = await _twitterClient.Tweets.PublishTweetAsync(tweetBuilder.ToString());
            _logger.LogInformation($"Tweet Id: {tweet.Id}");
            _logger.LogInformation($"{nameof(Tweet)} completed.");
        }
    }
}