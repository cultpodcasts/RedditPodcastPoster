using RedditPodcastPoster.Models.Catalogue;

namespace Api.Dtos.Mapping;

public static class SubjectMatchDtoMapper
{
    public static SubjectMatchDto ToDto(this PlayableSubjectMatch match) =>
        new()
        {
            Subject = match.Subject,
            Term = match.Term,
            Source = match.Source.ToString()
        };

    /// <summary>Preserves null (not recorded) versus empty (recorded, no evidence).</summary>
    public static List<SubjectMatchDto>? ToDtos(this IEnumerable<PlayableSubjectMatch>? matches) =>
        matches?.Select(ToDto).ToList();
}
