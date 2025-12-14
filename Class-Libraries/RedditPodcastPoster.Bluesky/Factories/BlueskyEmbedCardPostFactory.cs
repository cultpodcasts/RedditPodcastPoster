using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Providers;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text;
using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyEmbedCardPostFactory(
    ITextSanitiser textSanitiser,
    IHashTagEnricher hashTagEnricher,
    IHashTagProvider hashTagProvider,
    IOptions<BlueskyOptions> blueskyOptions,
    IEpisodeThumbnailProvider episodeThumbnailProvider,
    HttpClient httpClient,
    ILogger<IBlueskyEmbedCardPostFactory> logger
) : IBlueskyEmbedCardPostFactory
{
    private const int MinTitleLength = 10;
    public const string LengthFormat = @"\[h\:mm\:ss\]";
    public const string? ReleaseFormat = "d MMM yyyy";
    private readonly BlueskyOptions _blueskyOptions = blueskyOptions.Value;

    public async Task<BlueskyEmbedCardPost> Create(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var postModel = (podcastEpisode.Podcast, new[] { podcastEpisode.Episode }).ToPostModel();
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
                    hashtagsAdded.Add(hashtag.MatchingText?.ToLowerInvariant() ?? hashtag.Tag.ToLowerInvariant());
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
                .Where(x => !hashtagsAdded.Contains(x.ToLowerInvariant()))
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
            var min = Math.Min(episodeTitle.Length, permittedTitleLength - 1);
            if (min < MinTitleLength)
            {
                throw new InvalidOperationException(
                    $"Unable to form tweet body from '\"{episodeTitle}\"{Environment.NewLine}{tweetBuilder}', calculated title-length: {min} which is less than {MinTitleLength}.");
            }

            episodeTitle = episodeTitle[..min] + "…";
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
        else if (podcastEpisode.Episode.Urls.Apple != null)
        {
            url = podcastEpisode.Episode.Urls.Apple!;
            urlPodcastService = Service.Apple;
        }
        else if (podcastEpisode.Episode.Urls.InternetArchive != null)
        {
            url = podcastEpisode.Episode.Urls.InternetArchive!;
            urlPodcastService = Service.Other;
        }
        else if (podcastEpisode.Episode.Urls.BBC != null)
        {
            url = podcastEpisode.Episode.Urls.BBC!;
            urlPodcastService = Service.Other;
        }
        else
        {
            throw new InvalidOperationException(
                $"No url for podcast-id '${podcastEpisode.Podcast.Id}' and episode-id '${podcastEpisode.Episode.Images}'.");
        }

        var tweet = tweetBuilder.ToString();

        var thumbnail = await episodeThumbnailProvider.GetThumbnail(podcastEpisode, urlPodcastService);
        IReadOnlyCollection<Image>? images = null;
        if (thumbnail != null)
        {
            try
            {
                var request = await httpClient.GetAsync(thumbnail);
                var imageBytes = await request.Content.ReadAsByteArrayAsync();
                var imageMimeType = request.Content.Headers.ContentType?.MediaType;
                var image = string.IsNullOrWhiteSpace(imageMimeType)
                    ? new Image
                    {
                        Content = imageBytes
                    }
                    : new Image
                    {
                        Content = imageBytes,
                        MimeType = imageMimeType
                    };
                images = [image];
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get thumbnail from url '{url}'.", thumbnail);
            }
        }

        return new BlueskyEmbedCardPost(tweet, url, images);
    }
}