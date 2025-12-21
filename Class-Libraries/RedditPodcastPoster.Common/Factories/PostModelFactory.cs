using System.Globalization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.Episode;
using PostModel = RedditPodcastPoster.Models.PostModel;

namespace RedditPodcastPoster.Common.Factories;

public class PostModelFactory(
    ISubjectsProvider subjectsProvider,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PostModelFactory> logger) : IPostModelFactory
#pragma warning restore CS9113 // Parameter is unread.
{
    private IEnumerable<Subject> subjects = subjectsProvider.GetAll().ToBlockingEnumerable();

    public PostModel ToPostModel(
        (Podcast Podcast, IEnumerable<Episode> Episodes) podcastEpisodes,
        bool preferYouTube = false)
    {
        var subjectKnownTerms = podcastEpisodes.Episodes
            .SelectMany(x => x.Subjects).Distinct()
            .Select(x => subjects.SingleOrDefault(y => y.Name == x))
            .SelectMany(x => x?.KnownTerms ?? []).ToArray();
        var postModel = new PostModel(
            podcastEpisodes.Podcast.Name,
            podcastEpisodes.Podcast.TitleRegex,
            podcastEpisodes.Podcast.DescriptionRegex,
            podcastEpisodes.Episodes.Select(ToBasicEpisode),
            preferYouTube ? Service.YouTube : podcastEpisodes.Podcast.PrimaryPostService,
            podcastEpisodes.Podcast.KnownTerms ?? [],
            subjectKnownTerms
        );
        return postModel;
    }

    private static EpisodePost ToBasicEpisode(Episode episode)
    {
        var id = "unknown";
        if (!string.IsNullOrWhiteSpace(episode.SpotifyId))
        {
            id = $"Spotify-{episode.SpotifyId}";
        }
        else if (episode.AppleId != null)
        {
            id = $"Apple-{episode.AppleId}";
        }
        else if (!string.IsNullOrWhiteSpace(episode.YouTubeId))
        {
            id = $"YouTube-{episode.YouTubeId}";
        }

        return new EpisodePost(
            episode.Title,
            episode.Urls.YouTube,
            episode.Urls.Spotify,
            episode.Urls.Apple,
            episode.Release.ToString("d MMM yyyy"),
            episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture),
            episode.Description,
            id,
            episode.Release,
            episode.Subjects.ToArray(),
            episode.Urls.BBC,
            episode.Urls.InternetArchive);
    }
}