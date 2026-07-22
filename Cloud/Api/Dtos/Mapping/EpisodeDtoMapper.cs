using System.Text.RegularExpressions;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.Providers;
using RedditPodcastPoster.Text.Sanitisers;
using DomainEpisode = RedditPodcastPoster.Models.Episodes.Episode;
using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using DomainSubject = RedditPodcastPoster.Models.Subjects.Subject;

namespace Api.Dtos.Mapping;

public class EpisodeDtoMapper(
    ITextSanitiser textSanitiser,
    IPersonService personService,
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
            episodes.Add(await ToDto(pair.Episode, pair.Podcast, subjects));
        }

        return episodes;
    }

    public async Task<EpisodeDto> ToDto(
        DomainEpisode episode,
        DomainPodcast podcast,
        IEnumerable<DomainSubject> subjects,
        bool includeGuestSuggestions = false)
    {
        var episodeSubjects = subjects
            .Where(s => episode.Subjects.Contains(s.Name))
            .SelectMany(s => s.KnownTerms ?? Array.Empty<string>())
            .ToArray();

        var titleRegex = string.IsNullOrWhiteSpace(podcast.TitleRegex)
            ? null
            : new Regex(podcast.TitleRegex, RegexOptions.IgnoreCase);

        var descriptionRegex = string.IsNullOrWhiteSpace(podcast.DescriptionRegex)
            ? null
            : new Regex(podcast.DescriptionRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline);

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
            RemovedSubjects = episode.RemovedSubjects,
            Matches = episode.Matches,
            SearchTerms = episode.SearchTerms,
            YouTubePodcast = !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId),
            SpotifyPodcast = !string.IsNullOrWhiteSpace(podcast.SpotifyId),
            ApplePodcast = podcast.AppleId != null,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            Image =
                episode.Images?.YouTube ?? episode.Images?.Spotify ?? episode.Images?.Apple ?? episode.Images?.Other,
            Language = episode.Language,
            Guests = episode.Guests,
            DisplayTitle = await textSanitiser.SanitiseTitle(
                episode.Title,
                titleRegex,
                podcast.KnownTerms ?? Array.Empty<string>(),
                episodeSubjects),
            DisplayDescription = textSanitiser.SanitiseDescription(episode.Description, descriptionRegex)
        };

        if (episode.Guests is { Length: > 0 })
        {
            var guestPeople = await personService.GetByNames(episode.Guests);
            dto.GuestPeople = guestPeople.Select(x => x.ToDto()).ToList();
        }

        if (includeGuestSuggestions)
        {
            var selectedNames = episode.Guests?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            var suggestions = await personService.MatchEpisode(episode);
            dto.GuestSuggestions = suggestions
                .Where(x => !selectedNames.Contains(x.Person.Name))
                .Select(ToPersonMatch)
                .ToList();
        }

        return dto;
    }

    public static EpisodeDto.PersonMatch ToPersonMatch(PersonMatch match)
    {
        return new EpisodeDto.PersonMatch
        {
            Person = new PersonDto
            {
                Id = match.Person.Id,
                Name = match.Person.Name,
                TwitterHandle = match.Person.TwitterHandle,
                BlueskyHandle = match.Person.BlueskyHandle
            },
            MatchResults = match.MatchResults
                .Select(x => new EpisodeDto.MatchResult { Term = x.Term, Matches = x.Matches })
                .ToArray()
        };
    }
}
