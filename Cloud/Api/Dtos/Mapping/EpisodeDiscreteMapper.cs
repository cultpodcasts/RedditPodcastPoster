using System.Text.RegularExpressions;
using Api.Dtos.Extensions;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Text.Sanitisers;
using DomainEpisode = RedditPodcastPoster.Models.Episodes.Episode;
using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using DomainSubject = RedditPodcastPoster.Models.Subjects.Subject;

namespace Api.Dtos.Mapping;

public class EpisodeDiscreteMapper(
    ITextSanitiser textSanitiser,
    IPersonService personService)
{
    public async Task<DiscreteEpisode> ToDiscreteEpisode(
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

        var discreteEpisode = new DiscreteEpisode
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
            discreteEpisode.GuestPeople = guestPeople.Select(x => x.ToDto()).ToList();
        }

        if (includeGuestSuggestions)
        {
            var selectedNames = episode.Guests?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            var suggestions = await personService.MatchEpisode(episode);
            discreteEpisode.GuestSuggestions = suggestions
                .Where(x => !selectedNames.Contains(x.Person.Name))
                .Select(ToPersonMatchDto)
                .ToList();
        }

        return discreteEpisode;
    }

    public static PersonMatchDto ToPersonMatchDto(PersonMatch match)
    {
        return new PersonMatchDto
        {
            Person = new Person
            {
                Id = match.Person.Id,
                Name = match.Person.Name,
                TwitterHandle = match.Person.TwitterHandle,
                BlueskyHandle = match.Person.BlueskyHandle
            },
            MatchResults = match.MatchResults
                .Select(x => new PersonMatchResultDto { Term = x.Term, Matches = x.Matches })
                .ToArray()
        };
    }
}
