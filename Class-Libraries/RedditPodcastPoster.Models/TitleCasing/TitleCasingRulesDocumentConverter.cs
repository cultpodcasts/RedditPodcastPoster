using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.TitleCasing;

/// <summary>
/// Deserializes TitleCasingRules by <c>language</c>: <c>*</c> → universal, <c>en</c> → English, else non-English.
/// </summary>
public sealed class TitleCasingRulesDocumentConverter : JsonConverter<TitleCasingRulesDocument>
{
    public override TitleCasingRulesDocument? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!document.RootElement.TryGetProperty("language", out var languageProp) &&
            !document.RootElement.TryGetProperty("Language", out languageProp))
        {
            throw new JsonException("TitleCasingRules document requires a language property.");
        }

        var language = languageProp.GetString() ?? "";
        var normalised = string.IsNullOrWhiteSpace(language)
            ? ""
            : TitleCasingRulesDocument.NormaliseLanguage(language);

        var concreteOptions = CloneWithoutThisConverter(options);
        var json = document.RootElement.GetRawText();

        if (TitleCasingRulesDocument.IsUniversal(normalised))
        {
            return JsonSerializer.Deserialize<UniversalTitleCasingRulesDocument>(json, concreteOptions);
        }

        if (string.Equals(normalised, "en", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Deserialize<EnglishTitleCasingRulesDocument>(json, concreteOptions);
        }

        return JsonSerializer.Deserialize<NonEnglishTitleCasingRulesDocument>(json, concreteOptions);
    }

    public override void Write(
        Utf8JsonWriter writer,
        TitleCasingRulesDocument value,
        JsonSerializerOptions options)
    {
        var concreteOptions = CloneWithoutThisConverter(options);
        switch (value)
        {
            case UniversalTitleCasingRulesDocument universal:
                JsonSerializer.Serialize(writer, universal, concreteOptions);
                break;
            case EnglishTitleCasingRulesDocument english:
                JsonSerializer.Serialize(writer, english, concreteOptions);
                break;
            case NonEnglishTitleCasingRulesDocument nonEnglish:
                JsonSerializer.Serialize(writer, nonEnglish, concreteOptions);
                break;
            default:
                throw new JsonException(
                    $"Cannot serialize TitleCasingRules concrete type {value.GetType().Name}.");
        }
    }

    private static JsonSerializerOptions CloneWithoutThisConverter(JsonSerializerOptions options)
    {
        var clone = new JsonSerializerOptions(options);
        for (var i = clone.Converters.Count - 1; i >= 0; i--)
        {
            if (clone.Converters[i] is TitleCasingRulesDocumentConverter)
            {
                clone.Converters.RemoveAt(i);
            }
        }

        return clone;
    }
}
