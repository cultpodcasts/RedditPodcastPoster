using RedditPodcastPoster.Models;
using RedditPodcastPoster.Search;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

public static class PodcastEpisodeExtensions
{
    public static EpisodeSearchRecord ToEpisodeSearchRecord(this PodcastEpisode podcastEpisode)
    {
        var image = podcastEpisode.Episode.Images?.YouTube ?? podcastEpisode.Episode.Images?.Spotify ??
            podcastEpisode.Episode.Images?.Apple ?? podcastEpisode.Episode.Images?.Other;
        var podcastEpisodeDescription = podcastEpisode.Episode.Description.Trim();
        return new EpisodeSearchRecord
        {
            Apple = podcastEpisode.Episode.Urls.Apple != null
                ? podcastEpisode.Episode.Urls.Apple.ToString()
                : string.Empty,
            BBC = podcastEpisode.Episode.Urls.BBC != null ? podcastEpisode.Episode.Urls.BBC.ToString() : string.Empty,
            Duration = podcastEpisode.Episode.Length.ToString(),
            EpisodeDescription = podcastEpisodeDescription.Substring(0,
                Math.Min(Constants.DescriptionSize, podcastEpisodeDescription.Length)),
            EpisodeSearchTerms = podcastEpisode.Episode.SearchTerms ?? string.Empty,
            EpisodeTitle = podcastEpisode.Episode.Title.Trim(),
            Explicit = podcastEpisode.Episode.Explicit,
            Id = podcastEpisode.Episode.Id.ToString(),
            Image = image != null ? image.ToString() : string.Empty,
            InternetArchive = podcastEpisode.Episode.Urls.InternetArchive != null
                ? podcastEpisode.Episode.Urls.InternetArchive.ToString()
                : string.Empty,
            PodcastName = podcastEpisode.Podcast.Name.Trim(),
            PodcastSearchTerms = podcastEpisode.Podcast.SearchTerms ?? string.Empty,
            Release = podcastEpisode.Episode.Release,
            Spotify = podcastEpisode.Episode.Urls.Spotify != null
                ? podcastEpisode.Episode.Urls.Spotify.ToString()
                : string.Empty,
            Subjects = podcastEpisode.Episode.Subjects.ToArray(),
            Youtube = podcastEpisode.Episode.Urls.YouTube != null
                ? podcastEpisode.Episode.Urls.YouTube.ToString()
                : string.Empty
        };
    }
}