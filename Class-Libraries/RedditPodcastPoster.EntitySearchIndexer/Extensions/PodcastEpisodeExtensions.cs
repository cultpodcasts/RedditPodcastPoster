using RedditPodcastPoster.Models;
using RedditPodcastPoster.Search;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

public static class PodcastEpisodeExtensions
{
    public static EpisodeSearchRecord ToEpisodeSearchRecord(this PodcastEpisode podcastEpisode)
    {
        var image = podcastEpisode.Episode.Images?.YouTube ?? podcastEpisode.Episode.Images?.Spotify ??
            podcastEpisode.Episode.Images?.Apple ?? podcastEpisode.Episode.Images?.Other;
        var youtubeImageVariant = GetYoutubeImageVariant(image, podcastEpisode.Episode.YouTubeId);
        if (youtubeImageVariant != null)
        {
            image = null;
        }

        var podcastEpisodeDescription = podcastEpisode.Episode.Description.Trim();
        var duration = podcastEpisode.Episode.Length.ToString();
        return new EpisodeSearchRecord
        {
            AppleId = podcastEpisode.Episode.AppleId?.ToString(),
            BBC = podcastEpisode.Episode.Urls.BBC != null ? podcastEpisode.Episode.Urls.BBC.ToString() : string.Empty,
            Duration = duration.EndsWith(".0000000", StringComparison.Ordinal) ? duration[..^8] : duration,
            EpisodeDescription = podcastEpisodeDescription.Substring(0,
                Math.Min(Constants.DescriptionSize, podcastEpisodeDescription.Length)),
            EpisodeSearchTerms = podcastEpisode.Episode.SearchTerms ?? string.Empty,
            EpisodeTitle = podcastEpisode.Episode.Title.Trim(),
            Id = podcastEpisode.Episode.Id.ToString(),
            Image = image?.ToString(),
            InternetArchive = podcastEpisode.Episode.Urls.InternetArchive != null
                ? podcastEpisode.Episode.Urls.InternetArchive.ToString()
                : string.Empty,
            Lang = podcastEpisode.Episode.Language ?? podcastEpisode.Podcast.Language,
            PodcastAppleId = podcastEpisode.Podcast.AppleId?.ToString(),
            PodcastName = podcastEpisode.Podcast.Name.Trim(),
            PodcastSearchTerms = podcastEpisode.Podcast.SearchTerms ?? string.Empty,
            Release = podcastEpisode.Episode.Release,
            SpotifyId = NullIfWhiteSpace(podcastEpisode.Episode.SpotifyId),
            Subjects = podcastEpisode.Episode.Subjects.ToArray(),
            YoutubeId = NullIfWhiteSpace(podcastEpisode.Episode.YouTubeId),
            YoutubeImageVariant = youtubeImageVariant
        };
    }

    private static string? GetYoutubeImageVariant(Uri? image, string youtubeId)
    {
        if (image == null ||
            string.IsNullOrWhiteSpace(youtubeId) ||
            !image.Host.Equals("i.ytimg.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var expectedPrefix = $"/vi/{youtubeId}/";
        if (!image.AbsolutePath.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return image.AbsolutePath[expectedPrefix.Length..] switch
        {
            "maxresdefault.jpg" => "maxres",
            "sddefault.jpg" => "sd",
            "hqdefault.jpg" => "hq",
            _ => null
        };
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
