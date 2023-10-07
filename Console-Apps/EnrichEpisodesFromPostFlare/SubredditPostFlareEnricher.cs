using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.EnrichEpisodesFromPostFlare;

public class SubredditPostFlareEnricher
{
    private readonly IFileRepository _fileRepository;
    private readonly IPodcastRepository _podcastRepository;
    private readonly ILogger<CosmosDbRepository> _logger;
    private readonly RedditClient _redditClient;
    private readonly SubredditSettings _subredditSettings;
    private readonly Regex SpotifyUrl = new("episode/(?'episodeid'[a-zA-Z0-9]+)/?", RegexOptions.Compiled);
    private readonly Regex AppleUrl = new(@"\?i=(?'epsiodeid'\d+)", RegexOptions.Compiled);
    private readonly Regex YouTubeUrl = new(@"v=(?'episodeid'\w+)", RegexOptions.Compiled);
    private readonly Regex YouTubeShortUrl = new(@"/(?'episodeid'\w+)$", RegexOptions.Compiled);

    public SubredditPostFlareEnricher(
        IFileRepository fileRepository,
        IPodcastRepository podcastRepository,
        RedditClient redditClient,
        IOptions<SubredditSettings> subredditSettings,
        ILogger<CosmosDbRepository> logger)
    {
        _fileRepository = fileRepository;
        _podcastRepository = podcastRepository;
        _redditClient = redditClient;
        _subredditSettings = subredditSettings.Value;
        _logger = logger;
    }

    public async Task Run(bool createLocalRedditPostsRepository)
    {
        if (createLocalRedditPostsRepository)
        {
            var after = string.Empty;
            var redditPostBatch =
                _redditClient
                    .Subreddit(_subredditSettings.SubredditName).Posts
                    .GetNew(after, limit: 10)
                    .ToList();
            while (redditPostBatch.Any())
            {
                foreach (var post in redditPostBatch)
                {
                    var redditPost = new RedditPost
                    {
                        FullName = post.Fullname,
                        Author = post.Author,
                        RedditId = post.Id,
                        Created = post.Created,
                        Edited = post.Edited,
                        Removed = post.Removed,
                        Spam = post.Spam,
                        NSFW = post.NSFW,
                        UpVotes = post.UpVotes,
                        UpVoteRatio = post.UpvoteRatio,
                        Title = post.Title,
                        DownsVotes = post.DownVotes,
                        LinkFlairText = post.Listing.LinkFlairText,
                        Url = post.Listing.URL,
                        IsVideo = post.Listing.IsVideo,
                        Text = post.Listing.SelfText,
                        Html = post.Listing.SelfTextHTML
                    };
                    await _fileRepository.Write(redditPost.FullName, redditPost);
                }

                after = redditPostBatch.Last().Fullname;
                redditPostBatch = _redditClient.Subreddit(_subredditSettings.SubredditName).Posts
                    .GetNew(limit: 10, after: after);
            }
        }

        var redditPosts = await _fileRepository.GetAll<RedditPost>().ToArrayAsync();
        var podcasts = await _podcastRepository.GetAll().ToListAsync();

        foreach (var redditPost in redditPosts.Where(x => !string.IsNullOrWhiteSpace(x.LinkFlairText)))
        {
            var postUrl = new Uri(redditPost.Url, UriKind.RelativeOrAbsolute);
            if (postUrl.IsAbsoluteUri)
            {
                var host = postUrl.Host;
                if (host.Contains("spotify"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(SpotifyUrl, redditPost, podcasts, urls=> urls.Spotify);
                } else if (host.Contains("apple"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(AppleUrl, redditPost, podcasts, urls => urls.Apple);
                } else if (host.Contains("youtube"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(YouTubeUrl, redditPost, podcasts, urls => urls.YouTube);

                } else if (host.Contains("youtu.be"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(YouTubeShortUrl, redditPost, podcasts, urls => urls.YouTube);
                }
                else
                {
                    _logger.LogError($"Unknown podcast service host: {redditPost.Url}");
                }
            }
        }

        foreach (var podcast in podcasts)
        {
            await _podcastRepository.Update(podcast);

        }
    }

    private void ApplyFlareToMatchingPodcastServiceUrls(Regex regex, RedditPost redditPost, List<Podcast> podcasts,
        Func<ServiceUrls, Uri?> Accessor)
    {
        var idMatch = regex.Match(redditPost.Url);
        if (idMatch.Success)
        {
            var serviceId = idMatch.Groups["episodeid"].Value;
            if (!string.IsNullOrWhiteSpace(serviceId))
            {
                var matches = podcasts.SelectMany(podcast => podcast.Episodes.Where(episode =>
                {
                    if (Accessor(episode.Urls) == null)
                    {
                        return false;
                    }

                    return Accessor(episode.Urls)!.ToString().Contains(serviceId);
                }), (podcast, episode) => new {Podcast = podcast, Episode = episode});
                foreach (var match in matches)
                {
                    var group = redditPost.LinkFlairText.Trim();
                    if (!match.Episode.Subjects.Contains(group))
                    {
                        _logger.LogInformation($"{match.Podcast.Name} - {match.Episode.Title} = {group}");
                        match.Episode.Subjects.Add(group);
                    }
                }
            }
        }
    }
}