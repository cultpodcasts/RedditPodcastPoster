using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.RichText;
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.HashTags;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyEmbedCardPostFactory(
    ITextSanitiser textSanitiser,
    IHashTagEnricher hashTagEnricher,
    IHashTagProvider hashTagProvider,
    IPostModelFactory postModelFactory,
    IPersonGuestHandleResolver personGuestHandleResolver,
    IOptions<BlueskyOptions> blueskyOptions,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<IBlueskyEmbedCardPostFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IBlueskyEmbedCardPostFactory
{
    private const int MinTitleLength = 10;

    // Bluesky's limit is 300 graphemes. The budget counts UTF-16 chars, which is never fewer than graphemes,
    // so a post within this length is always within Bluesky's limit.
    private const int MaxPostLength = 300;
    public const string LengthFormat = @"\[h\:mm\:ss\]";
    public const string? ReleaseFormat = "d MMM yyyy";
    private readonly BlueskyOptions _blueskyOptions = blueskyOptions.Value;

    public async Task<BlueskyEmbedCardPost> Create(PodcastEpisode podcastEpisode, Uri? shortUrl, bool hasShareImage = false)
    {
        var postModel = postModelFactory.ToPostModel((podcastEpisode.Podcast, [podcastEpisode.Episode]));
        var episodeTitle = await textSanitiser.SanitiseTitle(postModel);

        var episodeHashtags = await hashTagProvider.GetHashTags(podcastEpisode.Episode.Subjects);
        episodeHashtags = episodeHashtags.Union(podcastEpisode.Podcast.GetHashTags()).ToList();
        if (!string.IsNullOrWhiteSpace(podcastEpisode.Episode.HashTag))
        {
            episodeHashtags = episodeHashtags.Union(podcastEpisode.Episode.HashTag.ToHashTags()).ToList();
        }
        if (!string.IsNullOrWhiteSpace(_blueskyOptions.HashTag))
        {
            episodeHashtags.Add(new HashTag(_blueskyOptions.HashTag, null));
        }

        var hashtagsAdded = new List<string>();
        foreach (var hashtag in episodeHashtags)
        {
            if (!hashtagsAdded.Select(x => x.ToLowerInvariant()).Contains(hashtag.Tag.ToLowerInvariant()))
            {
                // Multi-word enrichment tags: MatchingText holds the compacted form
                // ("Alpha Beta" → "#AlphaBeta"). Non-whitespace stored tags still gate on Tag
                // so Some-Tag is not rewritten as #SomeTag.
                var tagText = HashtagText.GetTitleEnrichmentTagText(hashtag.Tag, hashtag.MatchingText);
                if (tagText is null)
                {
                    continue;
                }

                (episodeTitle, var addedHashTag) =
                    hashTagEnricher.AddHashTag(
                        episodeTitle,
                        hashtag.Tag.TrimStart('#'),
                        tagText);
                if (addedHashTag)
                {
                    hashtagsAdded.Add(hashtag.Tag);
                }
            }
        }

        var podcastName = textSanitiser.SanitisePodcastName(postModel);

        var postBuilder = new StringBuilder();
        var (_, blueskyHandles) = await personGuestHandleResolver.Resolve(podcastEpisode.Episode);
        var guestHandlesToAppend = SocialHandleDeduplicator.Deduplicate(
            blueskyHandles,
            alreadyTagged: [podcastEpisode.Podcast.BlueskyHandle]);
        var guestHandles = guestHandlesToAppend.Length > 0
            ? " " + string.Join(" ", guestHandlesToAppend)
            : string.Empty;
        if (SocialPostKindTemplate.HasParentLine(podcastEpisode))
        {
            if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.BlueskyHandle))
            {
                postBuilder.AppendLine($"{podcastName} {podcastEpisode.Podcast.BlueskyHandle}{guestHandles}");
            }
            else
            {
                postBuilder.AppendLine($"{podcastName}{guestHandles}");
            }
        }
        else
        {
            // Film: no parent name; the film's own handle and guests still get tagged.
            var filmHandles = string.Join(" ",
                new[] { podcastEpisode.Podcast.BlueskyHandle }.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Concat(guestHandlesToAppend));
            if (!string.IsNullOrWhiteSpace(filmHandles))
            {
                postBuilder.AppendLine(filmHandles);
            }
        }

        postBuilder.AppendLine(SocialPostKindTemplate.ReleaseLine(podcastEpisode));

        var shortUrlOnly = hasShareImage && _blueskyOptions.ShortUrlOnlyWhenShareImage;
        var includeShortInText = shortUrlOnly
            || (_blueskyOptions.WithEpisodeUrl &&
                (SocialPostKindTemplate.AlwaysLinksSite(podcastEpisode) ||
                 podcastEpisode.HasMultipleServices() || podcastEpisode.Episode.Subjects.Any()));

        // Short URL is already in the skeleton when includeShortInText. Extra 26 was for dual-link
        // budgeting; short-URL-only share-image posts skip it and allow a longer title.
        var reserveSecondUrlBudget = !shortUrlOnly
            && _blueskyOptions.WithEpisodeUrl
            && (SocialPostKindTemplate.AlwaysLinksSite(podcastEpisode) ||
                 podcastEpisode.HasMultipleServices() || podcastEpisode.Episode.Subjects.Any());
        // The title is wrapped as "{title}" plus a line break, so those characters count against the limit too (#1097).
        var titleWrapperLength = 2 + Environment.NewLine.Length;
        var preamble = postBuilder.ToString();
        var enrichedTitle = episodeTitle;

        // Choose the end line from the exact title string that will be posted. Accept that candidate
        // only when it fits the budget just computed. Otherwise cut the original enriched title to the
        // budget and try the shorter string. The uncut title is posted only when the end line was
        // selected from it, so a cut that looks like a shorter tag cannot bring the uncut title back
        // with that tag linked nowhere.
        var candidate = enrichedTitle;
        var accepted = false;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var endHashTags = SelectEndHashTags(candidate, episodeHashtags);
            postBuilder.Clear();
            postBuilder.Append(preamble);
            if (!string.IsNullOrWhiteSpace(endHashTags))
            {
                postBuilder.AppendLine(endHashTags);
            }

            if (shortUrl != null && includeShortInText)
            {
                postBuilder.AppendLine($"{shortUrl.AbsoluteUri}");
            }

            var permittedTitleLength = MaxPostLength -
                                       (postBuilder.Length + titleWrapperLength + (reserveSecondUrlBudget ? 26 : 0));
            if (candidate.Length <= permittedTitleLength)
            {
                episodeTitle = candidate;
                accepted = true;
                break;
            }

            var min = Math.Min(enrichedTitle.Length, permittedTitleLength - 1);
            // Check the minimum on what the whole-text-element cut keeps, which can be shorter than the budget
            // when a long grapheme cluster straddles the cut.
            var truncatedTitle = BlueskyEmbedText.TruncateToWholeTextElements(enrichedTitle, min);
            if (truncatedTitle.Length < MinTitleLength)
            {
                throw new InvalidOperationException(
                    $"Unable to form tweet body from '\"{enrichedTitle}\"{Environment.NewLine}{postBuilder}', calculated title-length: {truncatedTitle.Length} which is less than {MinTitleLength}.");
            }

            var shortened = truncatedTitle + "…";
            if (shortened == candidate)
            {
                break;
            }

            candidate = shortened;
        }

        if (!accepted)
        {
            throw new InvalidOperationException(
                $"Unable to form tweet body from '\"{candidate}\"{Environment.NewLine}{postBuilder}' because the end hashtags were chosen from a different title.");
        }

        postBuilder.Insert(0, $"\"{episodeTitle}\"{Environment.NewLine}");
        if (!EpisodeServicePresence.TryGetPreferredSocialPostUrl(
                podcastEpisode.Episode,
                StreamingServiceCatalog.ImageCoalesceOrder,
                out var url,
                out var urlPodcastService))
        {
            throw new InvalidOperationException(
                $"No url for podcast-id '${podcastEpisode.Podcast.Id}' and episode-id '${podcastEpisode.Episode.Id}'.");
        }

        // Share-image shorts: the card opens the short URL. PlatformUrl is the YouTube or Spotify
        // URL the API card is built from. The short-url page is not fetched.
        Uri? platformUrl = null;
        if (shortUrlOnly && shortUrl != null)
        {
            platformUrl = url;
            url = shortUrl;
        }

        var tweet = postBuilder.ToString();
        return new BlueskyEmbedCardPost(tweet, url, urlPodcastService, platformUrl);
    }

    /// <summary>
    /// Tags already linked in <paramref name="title"/> stay off the end line. Bluesky treats tags as
    /// case-insensitive, and a tag that cannot be written unchanged is left out.
    /// </summary>
    private static string SelectEndHashTags(string title, IEnumerable<HashTag> episodeHashtags)
    {
        var linkedTags = new HashSet<string>(
            HashtagScanner.Find(title).Select(x => x.Tag), StringComparer.OrdinalIgnoreCase);
        return string.Join(" ",
            episodeHashtags
                .Where(x => x.MatchingText == null)
                .Select(x => x.Tag)
                .Distinct()
                .Where(HashtagText.IsReproducedAsIs)
                .Select(HashtagText.ToTagText)
                .OfType<string>()
                .Where(linkedTags.Add)
                .Select(x => $"#{x}"));
    }
}
