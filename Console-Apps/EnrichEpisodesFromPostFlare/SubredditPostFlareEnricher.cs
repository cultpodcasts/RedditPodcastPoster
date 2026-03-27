using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subreddit;

namespace EnrichEpisodesFromPostFlare;

public class SubredditPostFlareEnricher(
    ISubredditPostProvider subredditPostProvider,
    ISubredditRepository subredditRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<SubredditPostFlareEnricher> logger)
{
    private static readonly Regex AppleUrl = new(@"\?i=(?'epsiodeid'\d+)", RegexOptions.Compiled);
    private static readonly Regex SpotifyUrl = new("episode/(?'episodeid'[a-zA-Z0-9]+)/?", RegexOptions.Compiled);
    private static readonly Regex YouTubeShortUrl = new(@"/(?'episodeid'\w+)$", RegexOptions.Compiled);
    private static readonly Regex YouTubeUrl = new(@"v=(?'episodeid'\w+)", RegexOptions.Compiled);

    public async Task Run(bool createLocalRedditPostsRepository)
    {
        if (createLocalRedditPostsRepository)
        {
            var posts = subredditPostProvider.GetPosts().Select(x => x.ToRedditPost());
            foreach (var post in posts)
            {
                await subredditRepository.Save(post);
            }
        }

        var redditPosts = subredditRepository.GetAll();
        var podcasts = await podcastRepository.GetAll().ToListAsync();
        var podcastsById = podcasts.ToDictionary(x => x.Id, x => x);

        var episodes = new List<Episode>();
        foreach (var podcast in podcasts)
        {
            episodes.AddRange(await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync());
        }

        var updatedEpisodeIds = new HashSet<Guid>();

        await foreach (var redditPost in redditPosts.Where(x => !string.IsNullOrWhiteSpace(x.LinkFlairText)))
        {
            var postUrl = new Uri(redditPost.Url, UriKind.RelativeOrAbsolute);
            if (postUrl.IsAbsoluteUri)
            {
                var host = postUrl.Host;
                if (host.Contains("spotify"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(SpotifyUrl, redditPost, podcastsById, episodes,
                        urls => urls.Spotify, updatedEpisodeIds);
                }
                else if (host.Contains("apple"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(AppleUrl, redditPost, podcastsById, episodes,
                        urls => urls.Apple, updatedEpisodeIds);
                }
                else if (host.Contains("youtube"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(YouTubeUrl, redditPost, podcastsById, episodes,
                        urls => urls.YouTube, updatedEpisodeIds);
                }
                else if (host.Contains("youtu.be"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(YouTubeShortUrl, redditPost, podcastsById, episodes,
                        urls => urls.YouTube, updatedEpisodeIds);
                }
                else
                {
                    logger.LogError("Unknown podcast service host: {RedditPostUrl}", redditPost.Url);
                }
            }
        }

        foreach (var episode in episodes.Where(x => updatedEpisodeIds.Contains(x.Id)))
        {
            await episodeRepository.Save(episode);
        }
    }

    private void ApplyFlareToMatchingPodcastServiceUrls(
        Regex regex,
        RedditPost redditPost,
        IReadOnlyDictionary<Guid, Podcast> podcastsById,
        List<Episode> episodes,
        Func<ServiceUrls, Uri?> accessor,
        HashSet<Guid> updatedEpisodeIds)
    {
        var idMatch = regex.Match(redditPost.Url);
        if (idMatch.Success)
        {
            var serviceId = idMatch.Groups["episodeid"].Value;
            if (!string.IsNullOrWhiteSpace(serviceId))
            {
                var matches = episodes.Where(episode =>
                {
                    var serviceUrl = accessor(episode.Urls);
                    return serviceUrl != null && serviceUrl.ToString().Contains(serviceId);
                });

                foreach (var match in matches)
                {
                    var group = redditPost.LinkFlairText.Trim();
                    if (!match.Subjects.Contains(group))
                    {
                        var podcastName = podcastsById.TryGetValue(match.PodcastId, out var podcast)
                            ? podcast.Name
                            : match.PodcastName;

                        logger.LogInformation("{Name} - {Title} = {Group}", podcastName, match.Title, group);
                        match.Subjects.Add(group);
                        updatedEpisodeIds.Add(match.Id);
                    }
                }
            }
        }
    }
}