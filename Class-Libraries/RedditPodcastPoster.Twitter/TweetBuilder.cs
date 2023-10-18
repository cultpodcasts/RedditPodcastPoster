using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Twitter;

public class TweetBuilder : ITweetBuilder
{
    private readonly ILogger<TweetBuilder> _logger;
    private readonly ITextSanitiser _textSanitiser;

    public TweetBuilder(
        ITextSanitiser textSanitiser,
        ILogger<TweetBuilder> logger)
    {
        _textSanitiser = textSanitiser;
        _logger = logger;
    }

    public string BuildTweet(PodcastEpisode podcastEpisode)
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