using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subreddit;

namespace EnrichEpisodesFromPostFlare;

public class SubredditPostFlareEnricher(
    ISubredditPostProvider subredditPostProvider,
    ISubredditRepository subredditRepository,
    IPodcastRepository podcastRepository,
    ILogger<CosmosDbRepository> logger)
{
    private readonly Regex AppleUrl = new(@"\?i=(?'epsiodeid'\d+)", RegexOptions.Compiled);
    private readonly Regex SpotifyUrl = new("episode/(?'episodeid'[a-zA-Z0-9]+)/?", RegexOptions.Compiled);
    private readonly Regex YouTubeShortUrl = new(@"/(?'episodeid'\w+)$", RegexOptions.Compiled);
    private readonly Regex YouTubeUrl = new(@"v=(?'episodeid'\w+)", RegexOptions.Compiled);

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

        var redditPosts = await subredditRepository.GetAll();
        var podcasts = await podcastRepository.GetAll().ToListAsync();

        foreach (var redditPost in redditPosts.Where(x => !string.IsNullOrWhiteSpace(x.LinkFlairText)))
        {
            var postUrl = new Uri(redditPost.Url, UriKind.RelativeOrAbsolute);
            if (postUrl.IsAbsoluteUri)
            {
                var host = postUrl.Host;
                if (host.Contains("spotify"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(SpotifyUrl, redditPost, podcasts, urls => urls.Spotify);
                }
                else if (host.Contains("apple"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(AppleUrl, redditPost, podcasts, urls => urls.Apple);
                }
                else if (host.Contains("youtube"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(YouTubeUrl, redditPost, podcasts, urls => urls.YouTube);
                }
                else if (host.Contains("youtu.be"))
                {
                    ApplyFlareToMatchingPodcastServiceUrls(YouTubeShortUrl, redditPost, podcasts, urls => urls.YouTube);
                }
                else
                {
                    logger.LogError($"Unknown podcast service host: {redditPost.Url}");
                }
            }
        }

        foreach (var podcast in podcasts)
        {
            await podcastRepository.Update(podcast);
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
                        logger.LogInformation($"{match.Podcast.Name} - {match.Episode.Title} = {group}");
                        match.Episode.Subjects.Add(group);
                    }
                }
            }
        }
    }
}