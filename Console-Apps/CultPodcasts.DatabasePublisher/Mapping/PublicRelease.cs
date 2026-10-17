using System.Text.Json;
using RedditPodcastPoster.Models.Catalogue;

namespace CultPodcasts.DatabasePublisher.Mapping;

/// <summary>
/// The public text of a catalogue release. The catalogue stores a year as a JSON number, a date as
/// <c>yyyy-MM-dd</c> and a date-time as ISO-8601 Zulu; the public files of the new kinds always carry a string
/// so a consumer reads one type: <c>2019</c>, <c>2019-05-01</c> or <c>2019-05-01T12:00:00Z</c>. The text comes from
/// the catalogue's own converter, so it cannot drift from how the catalogue writes a release.
/// </summary>
public static class PublicRelease
{
    public static string? Format(CatalogueRelease? release) =>
        release is null ? null : JsonSerializer.Serialize(release).Trim('"');
}
