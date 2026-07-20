using RedditPodcastPoster.Models;
using RedditPodcastPoster.Search;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

public static class PodcastEpisodeExtensions
{
    public static EpisodeSearchRecord ToEpisodeSearchRecord(this PodcastEpisode podcastEpisode)
    {
        var image = SearchEpisodeImage.From(podcastEpisode.Episode.Images, podcastEpisode.Episode.YouTubeId);

        var podcastEpisodeDescription = podcastEpisode.Episode.Description.Trim();
        var duration = podcastEpisode.Episode.Length.ToString();
        return new EpisodeSearchRecord
        {
            AppleId = podcastEpisode.Episode.AppleId?.ToString(),
            BBC = podcastEpisode.Episode.Urls.BBC != null ? podcastEpisode.Episode.Urls.BBC.ToString() : string.Empty,
            Duration = duration.EndsWith(".0000000", StringComparison.Ordinal) ? duration[..^8] : duration,
            EpisodeDescription = DescriptionTruncator.TruncateForSearch(podcastEpisodeDescription),
            EpisodeSearchTerms = podcastEpisode.Episode.SearchTerms ?? string.Empty,
            EpisodeTitle = podcastEpisode.Episode.Title.Trim(),
            Id = podcastEpisode.Episode.Id.ToString(),
            Image = image.Image,
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
            // Compact tokens for the client to expand back to a full URL. Each is a non-null string
            // (empty when the winning image is not that platform's standard compactable form) so an
            // incremental Azure Search merge clears any stale token, leaving the client to render
            // whichever of image / youtubeImageVariant / spotifyImageId is populated.
            YoutubeImageVariant = image.YoutubeImageVariant,
            SpotifyImageId = image.SpotifyImageId
        };
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
