using System.Globalization;
using System.Text.Json;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace EpisodeWireRewrite.Tests;

internal static class WireCutoverTestJson
{
    private static readonly DomainTestFixture Fixture = new();

    public static (Guid EpisodeId, Guid PodcastId) NewIds() =>
        (Fixture.CreateGuid(), Fixture.CreateGuid());

    public static string LegacyDoc(
        Guid episodeId,
        Guid podcastId,
        string? searchTerms = null,
        string? language = null,
        long? metadataVersion = null,
        bool? removed = null,
        DateTime? releaseUtc = null,
        bool includeReleaseSort = false,
        string? publisherSearchTerms = null,
        string? extraPropertyValue = null)
    {
        releaseUtc ??= DomainTestFixture.UtcAtTime(-3, new TimeSpan(10, 11, 12));
        var releaseIso = releaseUtc.Value.ToString(
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", episodeId.ToString());
            writer.WriteString("podcastId", podcastId.ToString());
            writer.WriteNumber("type", 2);
            writer.WriteString("title", Fixture.CreateTitle());
            writer.WriteString("release", releaseIso);
            if (includeReleaseSort)
            {
                writer.WriteString(
                    "releaseSort",
                    releaseUtc.Value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
            }

            if (searchTerms is not null)
            {
                writer.WriteString("podcastSearchTerms", searchTerms);
            }

            if (language is not null)
            {
                writer.WriteString("podcastLanguage", language);
            }

            if (metadataVersion is not null)
            {
                writer.WriteNumber("podcastMetadataVersion", metadataVersion.Value);
            }

            if (removed is not null)
            {
                writer.WriteBoolean("podcastRemoved", removed.Value);
            }

            if (publisherSearchTerms is not null)
            {
                writer.WriteString("publisherSearchTerms", publisherSearchTerms);
            }

            if (extraPropertyValue is not null)
            {
                writer.WriteString("description", extraPropertyValue);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
