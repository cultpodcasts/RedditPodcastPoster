using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Reddit;

public class RedditPostTitleFactory(
    ITextSanitiser textSanitiser,
    IOptions<SubredditSettings> settings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<RedditPostTitleFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IRedditPostTitleFactory
{
    private readonly SubredditSettings _settings = settings.Value;

    public string ConstructPostTitle(PostModel postModel)
    {
        var title = ConstructBasePostTitle(postModel);
        var bundleSuffix = CreateBundleSuffix(postModel.BundledPartNumbers);
        var audioLinksSuffix = "";
        if (postModel.Link != postModel.Spotify &&
            postModel.HasYouTubeUrl &&
            (postModel.Spotify != null || postModel.Apple != null))
        {
            audioLinksSuffix = "(Audio links in comments)";
        }

        return ConstructFinalPostTitle(title, !string.IsNullOrWhiteSpace(postModel.EpisodeDescription), bundleSuffix,
            audioLinksSuffix);
    }

    private string ConstructFinalPostTitle(string title, bool hasDescription, string bundleSuffix,
        string audioLinksSuffix)
    {
        var ellipsisSuffix = hasDescription ? @"..""" : string.Empty;
        var terminalSuffix = hasDescription ? @"""" : string.Empty;
        if ((title + bundleSuffix + audioLinksSuffix).Length >
            _settings.SubredditTitleMaxLength - terminalSuffix.Length)
        {
            title =
                title.Substring(0,
                    _settings.SubredditTitleMaxLength -
                    (ellipsisSuffix.Length + bundleSuffix.Length + audioLinksSuffix.Length)) +
                $"{ellipsisSuffix}{bundleSuffix}{audioLinksSuffix}";
        }
        else
        {
            title += $"{terminalSuffix}{bundleSuffix}{audioLinksSuffix}";
        }

        return title;
    }

    private string ConstructBasePostTitle(PostModel postModel)
    {
        var episodeTitle = textSanitiser.SanitiseTitle(postModel);
        var podcastName = textSanitiser.SanitisePodcastName(postModel);
        var description = textSanitiser.SanitiseDescription(postModel);
        var title = $"\"{episodeTitle}\", {podcastName}, {postModel.ReleaseDate} {postModel.EpisodeLength}";
        if (!string.IsNullOrWhiteSpace(description))
        {
            title += $" \"{description}";
        }

        return title;
    }

    private static string CreateBundleSuffix(IEnumerable<int>? partNumbers)
    {
        var bundleSuffix = string.Empty;
        if (partNumbers == null || partNumbers.Count() <= 1)
        {
            return bundleSuffix;
        }

        var range = partNumbers.Count() switch
        {
            2 => $"Pt.{partNumbers.Last()}",
            3 => $"Pts.{partNumbers.Skip(1).First()}&{partNumbers.Last()}",
            _ => $"Pts.{partNumbers.Skip(1).First()}-{partNumbers.Last()}"
        };
        bundleSuffix = $"(Links to {range} in comments)";

        return bundleSuffix;
    }
}