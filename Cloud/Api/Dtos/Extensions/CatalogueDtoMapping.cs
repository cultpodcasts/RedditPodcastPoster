using System.Globalization;
using Api.Dtos.Mapping;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Dtos.Extensions;

/// <summary>Shared GET mapping for the publisher and playable fields of news, TV and film.</summary>
public static class CatalogueDtoMapping
{
    public static T WithPublisherFields<T>(this T dto, Publisher publisher) where T : PublisherFieldsDto
    {
        dto.Id = publisher.Id;
        dto.Name = publisher.Name;
        dto.Language = publisher.Language;
        dto.Removed = publisher.IsRemoved();
        dto.TwitterHandle = publisher.TwitterHandle;
        dto.BlueskyHandle = publisher.BlueskyHandle;
        dto.HashTag = publisher.HashTag;
        dto.EnrichmentHashTags = publisher.EnrichmentHashTags;
        dto.SearchTerms = publisher.SearchTerms;
        dto.KnownTerms = publisher.KnownTerms;
        dto.DefaultSubject = publisher.DefaultSubject;
        dto.IgnoredSubjects = publisher.IgnoredSubjects;
        dto.IgnoredAssociatedSubjects = publisher.IgnoredAssociatedSubjects;
        dto.LatestReleased = publisher.LatestReleased;
        dto.LastIndexed = publisher.LastIndexed;
        return dto;
    }

    public static T WithPlayableFields<T>(this T dto, Playable playable) where T : PlayableFieldsDto
    {
        dto.Id = playable.Id;
        dto.Title = playable.Title;
        dto.Description = playable.Description;
        dto.Release = FormatRelease(playable.Release);
        dto.Duration = FormatDuration(playable.Length);
        dto.Explicit = playable.Explicit;
        dto.Ignored = playable.Ignored;
        dto.Removed = playable.Removed;
        dto.Tweeted = playable.Tweeted;
        dto.BlueskyPosted = playable.BlueskyPosted;
        dto.Subjects = [.. playable.Subjects];
        dto.SearchTerms = playable.SearchTerms;
        dto.HashTag = playable.HashTag;
        dto.Language = playable.Language;
        dto.Guests = playable.Guests;
        dto.Services = playable.Services.ToDtos();
        return dto;
    }

    /// <summary><c>yyyy</c> for a year-only release (films), otherwise the calendar date <c>yyyy-MM-dd</c>.</summary>
    public static string? FormatRelease(CatalogueRelease? release) =>
        release switch
        {
            null => null,
            { Precision: CatalogueReleasePrecision.Year } => release.Year.ToString("D4", CultureInfo.InvariantCulture),
            _ => DateOnly.FromDateTime(release.ToSortUtc()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

    public static string FormatDuration(TimeSpan length) =>
        length.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
}
