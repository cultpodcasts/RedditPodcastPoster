using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyEmbedCardPostFactory(
    ITextSanitiser textSanitiser,
    IHashTagEnricher hashTagEnricher,
    IHashTagProvider hashTagProvider,
    IOptions<BlueskyOptions> blueskyOptions,
    ILogger<IBlueskyEmbedCardPostFactory> logger
) : IBlueskyEmbedCardPostFactory
{
    public const string LengthFormat = @"\[h\:mm\:ss\]";
    public const string? ReleaseFormat = "d MMM yyyy";
    private readonly BlueskyOptions _blueskyOptions = blueskyOptions.Value;

    public async Task<BlueskyEmbedCardPost> Create(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var postModel = (podcastEpisode.Podcast, new[] {podcastEpisode.Episode}).ToPostModel();
        var episodeTitle = textSanitiser.SanitiseTitle(postModel);

        var episodeHashtags = await hashTagProvider.GetHashTags(podcastEpisode.Episode.Subjects);
        if (!string.IsNullOrWhiteSpace(_blueskyOptions.HashTag))
        {
            episodeHashtags.Add(new HashTag(_blueskyOptions.HashTag, null));
        }

        var hashtagsAdded = new List<string>();
        foreach (var hashtag in episodeHashtags)
        {
            if (!hashtagsAdded.Select(x => x.ToLowerInvariant()).Contains(hashtag.Tag.ToLowerInvariant()))
            {
                (episodeTitle, var addedHashTag) =
                    hashTagEnricher.AddHashTag(
                        episodeTitle,
                        hashtag.Tag.TrimStart('#'),
                        hashtag.MatchingText?.TrimStart('#'));
                if (addedHashTag)
                {
                    hashtagsAdded.Add(hashtag.MatchingText ?? hashtag.Tag);
                }
            }
        }

        var podcastName = textSanitiser.SanitisePodcastName(postModel);

        var tweetBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.BlueskyHandle))
        {
            tweetBuilder.AppendLine($"{podcastName} {podcastEpisode.Podcast.BlueskyHandle}");
        }
        else
        {
            tweetBuilder.AppendLine($"{podcastName}");
        }

        tweetBuilder.AppendLine(
            $"{podcastEpisode.Episode.Release.ToString(ReleaseFormat)} {podcastEpisode.Episode.Length.ToString(LengthFormat, CultureInfo.InvariantCulture)}");

        var endHashTags = string.Join(" ",
            episodeHashtags
                .Where(x => x.MatchingText == null)
                .Select(x => x.Tag)
                .Distinct()
                .Where(x => !hashtagsAdded.Contains(x))
                .Select(x => $"#{x.TrimStart('#')}"));
        if (!string.IsNullOrWhiteSpace(endHashTags))
        {
            tweetBuilder.AppendLine(endHashTags);
        }

        if (shortUrl != null &&
            _blueskyOptions.WithEpisodeUrl &&
            (podcastEpisode.HasMultipleServices() ||
             podcastEpisode.Podcast.Episodes.Count > 1 ||
             podcastEpisode.Episode.Subjects.Any()))
        {
            tweetBuilder.AppendLine($"{shortUrl}");
        }

        var permittedTitleLength = 300 - (tweetBuilder.Length + (_blueskyOptions.WithEpisodeUrl ? 26 : 0));

        if (episodeTitle.Length > permittedTitleLength)
        {
            episodeTitle = episodeTitle[..Math.Min(episodeTitle.Length, permittedTitleLength - 1)] + "…";
        }

        tweetBuilder.Insert(0, $"\"{episodeTitle}\"{Environment.NewLine}");
        Uri url;
        Service urlPodcastService;
        if (podcastEpisode.Episode.Urls.YouTube != null)
        {
            url = podcastEpisode.Episode.Urls.YouTube;
            urlPodcastService = Service.YouTube;
        }
        else if (podcastEpisode.Episode.Urls.Spotify != null)
        {
            url = podcastEpisode.Episode.Urls.Spotify;
            urlPodcastService = Service.Spotify;
        }
        else
        {
            url = podcastEpisode.Episode.Urls.Apple!;
            urlPodcastService = Service.Apple;
        }

        var tweet = tweetBuilder.ToString();
        return new BlueskyEmbedCardPost(tweet, url, urlPodcastService);
    }
}