using Newtonsoft.Json;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Serialization;

/// <summary>
/// Deserializes Spotify episode DTOs into subclasses that include <c>restrictions</c>,
/// which SpotifyAPI.Web 7.4.2 does not model on <see cref="SimpleEpisode"/> / <see cref="FullEpisode"/>.
/// </summary>
public sealed class SpotifyEpisodeRestrictionsConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) =>
        objectType == typeof(SimpleEpisode) || objectType == typeof(FullEpisode);

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
        throw new NotSupportedException();

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer)
    {
        var targetType = objectType == typeof(FullEpisode)
            ? typeof(FullEpisodeWithRestrictions)
            : typeof(SimpleEpisodeWithRestrictions);

        return serializer.Deserialize(reader, targetType);
    }
}
