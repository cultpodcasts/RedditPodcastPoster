namespace Api.Dtos;

/// <summary>
/// Why a subject matched (<c>source</c> on <see cref="SubjectMatchDto"/>). Names and numeric values mirror the
/// domain <c>RedditPodcastPoster.Models.Subjects.SubjectMatchSource</c>, and the property writes it with
/// <c>JsonStringEnumConverter</c>, so the JSON text is the same as the string it replaced (#1513).
/// </summary>
public enum SubjectMatchSourceDto
{
    Title = 1,
    Description = 2,
    /// <summary>Subject applied because it is the podcast's DefaultSubject (no title/description term hit).</summary>
    PodcastDefault = 3
}
