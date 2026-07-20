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

        // The image field is coalesced YouTube-first. When the coalesced image is a standard
        // i.ytimg.com thumbnail it is encoded compactly as youtubeImageVariant and the image
        // URL is dropped (the client reconstructs it). The dropped value MUST be emitted as an
        // empty string rather than null: Azure AI Search cannot distinguish a null source value
        // from a missing field, so a null is ignored on merge and a previously-indexed image
        // (e.g. stale Spotify cover art from before a YouTube merge) is never cleared. Emitting a
        // non-null empty string forces the merge to overwrite/clear the stale value.
        var imageValue = youtubeImageVariant != null
            ? string.Empty
            : image?.ToString() ?? string.Empty;

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
            Image = imageValue,
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
