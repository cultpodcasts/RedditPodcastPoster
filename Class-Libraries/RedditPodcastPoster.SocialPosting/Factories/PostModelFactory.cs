using System.Globalization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Providers;

namespace RedditPodcastPoster.SocialPosting.Factories;

public class PostModelFactory(
    ISubjectsProvider subjectsProvider,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PostModelFactory> logger) : IPostModelFactory
#pragma warning restore CS9113 // Parameter is unread.
{
    private readonly IEnumerable<Subject> subjects = subjectsProvider.GetAll().ToBlockingEnumerable().ToList();

    public PostModel ToPostModel(
        (Podcast Podcast, IEnumerable<Episode> Episodes) podcastEpisodes,
        bool preferYouTube = false)
    {
        var subjectKnownTerms = podcastEpisodes.Episodes
            .SelectMany(x => x.Subjects).Distinct()
            .Select(x => subjects.SingleOrDefault(y => y.Name == x))
            .SelectMany(x => x?.KnownTerms ?? []).ToArray();
        var firstEpisode = podcastEpisodes.Episodes.First();
        var postModel = new PostModel(
            podcastEpisodes.Podcast.Name,
            podcastEpisodes.Podcast.TitleRegex,
            podcastEpisodes.Podcast.DescriptionRegex,
            podcastEpisodes.Episodes.Select(ToBasicEpisode),
            preferYouTube ? Service.YouTube : podcastEpisodes.Podcast.PrimaryPostService,
            podcastEpisodes.Podcast.KnownTerms ?? [],
            subjectKnownTerms,
            firstEpisode.Language
        );
        return postModel;
    }

    private static EpisodePost ToBasicEpisode(Episode episode)
    {
        var id = "unknown";
        var spotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode);
        var appleId = EpisodeServicePresence.AppleEpisodeId(episode);
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode);
        if (!string.IsNullOrWhiteSpace(spotifyId))
        {
            id = $"Spotify-{spotifyId}";
        }
        else if (appleId != null)
        {
            id = $"Apple-{appleId}";
        }
        else if (!string.IsNullOrWhiteSpace(youTubeId))
        {
            id = $"YouTube-{youTubeId}";
        }

        return new EpisodePost(
            episode.Title,
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube),
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify),
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple),
            episode.Release.ToString("d MMM yyyy"),
            episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture),
            episode.Description,
            id,
            episode.Release,
            episode.Subjects.ToArray(),
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer) ??
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcSounds),
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.InternetArchive));
    }
}