using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Twitter;

public class TweetBuilder : ITweetBuilder
{
    private readonly IHashTagEnricher _hashTagEnricher;
    private readonly ILogger<TweetBuilder> _logger;
    private readonly ISubjectRepository _subjectRepository;
    private readonly ITextSanitiser _textSanitiser;
    private readonly TwitterOptions _twitterOptions;

    public TweetBuilder(
        ITextSanitiser textSanitiser,
        ISubjectRepository subjectRepository,
        IHashTagEnricher hashTagEnricher,
        IOptions<TwitterOptions> twitterOptions,
        ILogger<TweetBuilder> logger)
    {
        _textSanitiser = textSanitiser;
        _subjectRepository = subjectRepository;
        _hashTagEnricher = hashTagEnricher;
        _twitterOptions = twitterOptions.Value;
        _logger = logger;
    }

    public async Task<string> BuildTweet(PodcastEpisode podcastEpisode)
    {
        var postModel = (podcastEpisode.Podcast, new[] {podcastEpisode.Episode}).ToPostModel();
        var episodeTitle = _textSanitiser.SanitiseTitle(postModel);
        var addedHashTag = false;
        if (!string.IsNullOrWhiteSpace(_twitterOptions.HashTag))
        {
            (episodeTitle, addedHashTag) = _hashTagEnricher.AddHashTag(episodeTitle, _twitterOptions.HashTag);
        }

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

        var hashTags = await GetHashTags(podcastEpisode.Episode.Subjects);
        if (!addedHashTag && !string.IsNullOrWhiteSpace(_twitterOptions.HashTag))
        {
            if (hashTags != null)
            {
                hashTags += $" #{_twitterOptions.HashTag}";
            }
            else
            {
                hashTags = $"#{_twitterOptions.HashTag}";
            }
        }

        tweetBuilder.AppendLine(hashTags);

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

    private async Task<string?> GetHashTags(List<string> episodeSubjects)
    {
        var subjectRetrieval = episodeSubjects.Select(x => _subjectRepository.GetByName(x)).ToArray();
        var subjects = await Task.WhenAll(subjectRetrieval);
        IEnumerable<string> hashTags = subjects.Where(x => !string.IsNullOrWhiteSpace(x?.HashTag))
            .Select(x => x!.HashTag).Distinct()!;
        if (hashTags.Any())
        {
            return string.Join(" ", hashTags);
        }

        return null;
    }
}