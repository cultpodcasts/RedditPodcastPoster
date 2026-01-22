using Api.Dtos;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Text;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace Api.Extensions;

public static class EpisodeExtensions
{
    extension(Episode episode)
    {
        public async Task<DiscreteEpisode> Enrich(Podcast podcast, ITextSanitiser textSanitiser, ISubjectRepository subjectRepository)
        {
            var episodeSubjects = (await subjectRepository.GetAllBy(x => episode.Subjects != null && episode.Subjects.Contains(x.Name), x => x.KnownTerms).ToListAsync()).SelectMany(x => x ?? Array.Empty<string>()).ToArray();
            return new DiscreteEpisode
            {
                Id = episode.Id,
                PodcastName = podcast.Name,
                Title = episode.Title,
                Description = episode.Description,
                Posted = episode.Posted,
                Tweeted = episode.Tweeted,
                BlueskyPosted = episode.BlueskyPosted,
                Ignored = episode.Ignored,
                Release = episode.Release,
                Removed = episode.Removed,
                Length = episode.Length,
                Explicit = episode.Explicit,
                SpotifyId = episode.SpotifyId,
                AppleId = episode.AppleId,
                YouTubeId = episode.YouTubeId,
                Urls = episode.Urls,
                Images = episode.Images,
                Subjects = episode.Subjects,
                SearchTerms = episode.SearchTerms,
                YouTubePodcast = !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId),
                SpotifyPodcast = !string.IsNullOrWhiteSpace(podcast.SpotifyId),
                ApplePodcast = podcast.AppleId != null,
                ReleaseAuthority = podcast.ReleaseAuthority,
                PrimaryPostService = podcast.PrimaryPostService,
                Image = episode.Images?.YouTube ??
                        episode.Images?.Spotify ?? episode.Images?.Apple ?? episode.Images?.Other,
                Language = episode.Language,
                TwitterHandles = episode.TwitterHandles,
                BlueskyHandles = episode.BlueskyHandles,
                DisplayTitle = textSanitiser.SanitiseTitle(
                    episode.Title,
                    string.IsNullOrWhiteSpace(podcast.TitleRegex)
                        ? null
                        : new System.Text.RegularExpressions.Regex(
                            podcast.TitleRegex,
                            Podcast.TitleFlags),
                    podcast.KnownTerms ?? Array.Empty<string>(),
                    episodeSubjects),
                DisplayDescription = textSanitiser.SanitiseDescription(episode.Description,
                    string.IsNullOrWhiteSpace(podcast.DescriptionRegex)
                        ? null
                        : new System.Text.RegularExpressions.Regex(
                            podcast.DescriptionRegex,
                            Podcast.DescriptionFlags))
            };
        }

        public PublicEpisode EnrichPublic(Podcast podcast)
        {
            return new PublicEpisode
            {
                Id = episode.Id,
                PodcastName = podcast.Name,
                Title = episode.Title,
                Description = episode.Description,
                Release = episode.Release,
                Length = episode.Length,
                Explicit = episode.Explicit,
                Urls = episode.Urls,
                Subjects = episode.Subjects,
                Image = episode.Images?.YouTube ??
                        episode.Images?.Spotify ?? episode.Images?.Apple ?? episode.Images?.Other
            };
        }
    }
}