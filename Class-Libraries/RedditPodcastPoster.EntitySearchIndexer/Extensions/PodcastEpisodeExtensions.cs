using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

public static class PodcastEpisodeExtensions
{
    public static EpisodeSearchRecord ToEpisodeSearchRecord(this PodcastEpisode podcastEpisode)
    {
        EpisodeServicePresence.NormalizeCatalog(podcastEpisode.Episode);
        var image = SearchEpisodeImage.From(podcastEpisode.Episode);

        var podcastEpisodeDescription = podcastEpisode.Episode.Description.Trim();
        var duration = podcastEpisode.Episode.Length.ToString();
        return new EpisodeSearchRecord
        {
            AppleId = EpisodeServicePresence.AppleEpisodeId(podcastEpisode.Episode)?.ToString(),
            BBC = BbcSearchField(podcastEpisode.Episode),
            Duration = duration.EndsWith(".0000000", StringComparison.Ordinal) ? duration[..^8] : duration,
            EpisodeDescription = DescriptionTruncator.TruncateForSearch(podcastEpisodeDescription),
            EpisodeSearchTerms = podcastEpisode.Episode.SearchTerms ?? string.Empty,
            EpisodeTitle = podcastEpisode.Episode.Title.Trim(),
            Id = podcastEpisode.Episode.Id.ToString(),
            Image = image.Image,
            InternetArchive = EpisodeServicePresence.TryGetUrl(podcastEpisode.Episode, ServiceKeys.InternetArchive)
                ?.ToString() ?? string.Empty,
            // Episode.Language only — null means English. Do not fall back to podcast language
            // (that undid curator "English" / "No Language" clears on non-English shows).
            // See docs/episode-language.md.
            Lang = NullIfWhiteSpace(EpisodeLanguageResolution.ForEpisode(podcastEpisode.Episode)),
            PodcastAppleId = podcastEpisode.Podcast.AppleId?.ToString(),
            PodcastName = podcastEpisode.Podcast.Name.Trim(),
            PodcastSearchTerms = podcastEpisode.Podcast.SearchTerms ?? string.Empty,
            Release = podcastEpisode.Episode.Release,
            Svc = SearchEpisodeServices.Compact(podcastEpisode.Episode.Services),
            SpotifyId = NullIfWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(podcastEpisode.Episode)),
            Subjects = podcastEpisode.Episode.Subjects.ToArray(),
            YoutubeId = NullIfWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(podcastEpisode.Episode))
        };
    }

    private static string BbcSearchField(Episode episode) =>
        (EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer) ??
         EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcSounds))?.ToString() ?? string.Empty;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
