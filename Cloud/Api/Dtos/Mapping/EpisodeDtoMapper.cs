using System.Text.RegularExpressions;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Subjects.Providers;
using RedditPodcastPoster.Text.Sanitisers;
using DomainEpisode = RedditPodcastPoster.Models.Episodes.Episode;
using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using DomainSubject = RedditPodcastPoster.Models.Subjects.Subject;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace Api.Dtos.Mapping;

public class EpisodeDtoMapper(
    ITextSanitiser textSanitiser,
    IPlayableGuestDtoEnricher guestDtoEnricher,
    ICachedSubjectProvider subjectsProvider)
{
    public async Task<IReadOnlyList<EpisodeDto>> ToDtos(
        IEnumerable<EpisodePodcastPair> pairs,
        CancellationToken cancellationToken)
    {
        var subjects = await subjectsProvider.GetAll().ToListAsync(cancellationToken);
        var episodes = new List<EpisodeDto>();
        foreach (var pair in pairs)
        {
            episodes.Add(await ToDto(pair.Episode, pair.Podcast, subjects, cancellationToken: cancellationToken));
        }

        return episodes;
    }

    public async Task<EpisodeDto> ToDto(
        DomainEpisode episode,
        DomainPodcast podcast,
        IEnumerable<DomainSubject> subjects,
        bool includeGuestSuggestions = false,
        CancellationToken cancellationToken = default)
    {
        var episodeSubjects = subjects
            .Where(s => episode.Subjects.Contains(s.Name))
            .SelectMany(s => s.KnownTerms ?? Array.Empty<string>())
            .ToArray();

        // A news report / TV episode arrives as a post projection whose transient podcast carries the parent's
        // indexing.titleRegex, so this is the display path for every kind (#1378).
        var titleRegex = DomainPodcast.CreateTitleRegex(podcast.TitleRegex);

        var descriptionRegex = DescriptionRegexTokens.Create(podcast.DescriptionRegex, episode.Title, podcast.Name);

        EpisodeServicePresence.NormalizeCatalog(episode);
        var dto = new EpisodeDto
        {
            Id = episode.Id,
            PodcastName = podcast.Name,
            PodcastId = podcast.Id,
            Title = episode.Title,
            Description = episode.Description,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Release = episode.ReleaseUtc ,
            Removed = episode.IsRemoved(),
            Length = episode.Length,
            Explicit = episode.Explicit,
            SpotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode) ?? string.Empty,
            AppleId = EpisodeServicePresence.AppleEpisodeId(episode),
            YouTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode) ?? string.Empty,
            Ids = episode.Ids.ToDto(),
            Urls = EpisodeServicePresence.ToServiceUrls(episode).ToDto(),
            Images = EpisodeServicePresence.ToEpisodeImages(episode).ToDto(),
            Services = episode.Services.ToDtos(),
            Subjects = episode.Subjects,
            RemovedSubjects = episode.RemovedSubjects,
            Matches = episode.Matches.ToDtos() ?? [],
            SearchTerms = episode.SearchTerms,
            HashTag = episode.HashTag,
            YouTubePodcast = podcast.HasYouTubeSource(),
            SpotifyPodcast = !string.IsNullOrWhiteSpace(podcast.SpotifyId),
            ApplePodcast = podcast.AppleId != null,
            ReleaseAuthority = podcast.ReleaseAuthority.ToDto(),
            PrimaryPostService = podcast.PrimaryPostService.ToDto(),
            Image =
                EpisodeServicePresence.CoalescedImage(episode, StreamingServiceCatalog.ImageCoalesceOrder),
            Language = episode.Language,
            Guests = episode.Guests,
            DisplayTitle = await textSanitiser.SanitiseTitle(
                episode.Title,
                titleRegex,
                podcast.KnownTerms ?? Array.Empty<string>(),
                episodeSubjects,
                episode.Language),
            DisplayDescription = textSanitiser.SanitiseDescription(episode.Description, descriptionRegex)
        };

        // Guest people / suggestions: shared with catalogue playable GETs via IPlayableGuestDtoEnricher.
        await guestDtoEnricher.EnrichAsync(
            dto,
            episode.Title,
            episode.Description,
            episode.Language,
            episode.Guests,
            includeGuestSuggestions: includeGuestSuggestions,
            cancellationToken: cancellationToken);

        return dto;
    }

    public static EpisodeDto.PersonMatch ToPersonMatch(PersonMatch match)
    {
        return new EpisodeDto.PersonMatch
        {
            Person = match.Person.ToDto(),
            MatchResults = match.MatchResults
                .Select(x => new EpisodeDto.MatchResult { Term = x.Term, Matches = x.Matches })
                .ToArray()
        };
    }
}
