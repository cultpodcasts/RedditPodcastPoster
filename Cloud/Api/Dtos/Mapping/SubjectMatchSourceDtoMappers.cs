using RedditPodcastPoster.Models.Subjects;

namespace Api.Dtos.Mapping;

/// <summary>Maps the domain subject-match source to <see cref="SubjectMatchSourceDto"/>; throws on an unmapped value so drift fails loudly.</summary>
public static class SubjectMatchSourceDtoMappers
{
    public static SubjectMatchSourceDto ToDto(this SubjectMatchSource source) =>
        source switch
        {
            SubjectMatchSource.Title => SubjectMatchSourceDto.Title,
            SubjectMatchSource.Description => SubjectMatchSourceDto.Description,
            SubjectMatchSource.PodcastDefault => SubjectMatchSourceDto.PodcastDefault,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unmapped SubjectMatchSource value.")
        };
}
