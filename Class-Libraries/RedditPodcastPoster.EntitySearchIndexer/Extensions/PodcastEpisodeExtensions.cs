using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

public static class PodcastEpisodeExtensions
{
    /// <param name="includeUnifiedPlayableFields">
    /// When false (the hourly indexer default), <c>contentKind</c>, <c>title</c>, <c>seriesName</c>,
    /// <c>description</c>, and <c>seriesDescription</c> stay null and are omitted from the upload.
    /// The live index does not have those fields yet; sending them fails every merge.
    /// Turn this on only after CreateSearchIndex has added the fields. The same
    /// <see cref="DescriptionTruncator"/> cap is what the Cosmos pull projects.
    /// </param>
    public static EpisodeSearchRecord ToEpisodeSearchRecord(
        this PodcastEpisode podcastEpisode,
        bool includeUnifiedPlayableFields = false)
    {
        EpisodeServicePresence.NormalizeCatalog(podcastEpisode.Episode);
        var image = SearchEpisodeImage.From(podcastEpisode.Episode);

        var podcastEpisodeDescription = podcastEpisode.Episode.Description.Trim();
        var truncatedDescription = DescriptionTruncator.TruncateForSearch(podcastEpisodeDescription);
        var duration = podcastEpisode.Episode.Length.ToString();
        return new EpisodeSearchRecord
        {
            AppleId = EpisodeServicePresence.AppleEpisodeId(podcastEpisode.Episode)?.ToString(),
            BBC = BbcSearchField(podcastEpisode.Episode),
            ContentKind = includeUnifiedPlayableFields ? SearchContentKind.Episode : null,
            Description = includeUnifiedPlayableFields ? truncatedDescription : null,
            Duration = duration.EndsWith(".0000000", StringComparison.Ordinal) ? duration[..^8] : duration,
            EpisodeDescription = truncatedDescription,
            EpisodeSearchTerms = podcastEpisode.Episode.SearchTerms ?? string.Empty,
            EpisodeTitle = podcastEpisode.Episode.Title.Trim(),
            SeriesDescription = includeUnifiedPlayableFields
                ? DescriptionTruncator.TruncateForSearch(podcastEpisode.Podcast.Description)
                : null,
            SeriesName = includeUnifiedPlayableFields ? podcastEpisode.Podcast.Name.Trim() : null,
            Title = includeUnifiedPlayableFields ? podcastEpisode.Episode.Title.Trim() : null,
            Id = podcastEpisode.Episode.Id.ToString(),
            Image = image.Image,
            InternetArchive = EpisodeServicePresence.TryGetUrl(podcastEpisode.Episode, StreamingServiceWire.ToKey(StreamingService.InternetArchive))
                ?.ToString() ?? string.Empty,
            // Episode.Language only — null means English. Do not fall back to podcast language
            // (that undid curator "English" / "No Language" clears on non-English shows).
            // See docs/episode-language.md.
            Lang = NullIfWhiteSpace(EpisodeLanguageResolution.ForEpisode(podcastEpisode.Episode)),
            PodcastAppleId = podcastEpisode.Podcast.AppleId?.ToString(),
            PodcastName = podcastEpisode.Podcast.Name.Trim(),
            PublisherSearchTerms = podcastEpisode.Podcast.SearchTerms ?? string.Empty,
            Release = podcastEpisode.Episode.ReleaseUtc,
            Svc = SearchEpisodeServices.Compact(podcastEpisode.Episode.Services),
            SpotifyId = NullIfWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(podcastEpisode.Episode)),
            Subjects = podcastEpisode.Episode.Subjects.ToArray(),
            YoutubeId = NullIfWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(podcastEpisode.Episode))
        };
    }

    private static string BbcSearchField(Episode episode) =>
        (EpisodeServicePresence.TryGetUrl(episode, StreamingServiceWire.ToKey(StreamingService.BbcIplayer)) ??
         EpisodeServicePresence.TryGetUrl(episode, StreamingServiceWire.ToKey(StreamingService.BbcSounds)))?.ToString() ?? string.Empty;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
