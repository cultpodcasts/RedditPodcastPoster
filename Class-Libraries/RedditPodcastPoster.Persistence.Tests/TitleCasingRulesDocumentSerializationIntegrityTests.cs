using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Repositories;
using RedditPodcastPoster.Persistence.Serialization;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// INTEGRITY: polymorphic TitleCasingRules members must survive every production STJ path
/// (Cosmos options, file I/O, CosmosSystemTextJsonSerializer adapter).
/// </summary>
public class TitleCasingRulesDocumentSerializationIntegrityTests
{
    private static IJsonSerializerOptionsProvider OptionsProvider { get; } = new JsonSerializerOptionsProvider();

    private static JsonSerializerOptions ProductionOptions() =>
        OptionsProvider.GetJsonSerializerOptions();

    [Fact(DisplayName =
        "INTEGRITY TitleCasingRules JSON (production options): when serializing as TitleCasingRulesDocument, then NonEnglish round-trip keeps knownTerms, lowerCaseTerms, and ignoredSubjects, because Cosmos downloader/uploader and attribute converter must not drop inherited members.")]
    public void production_options_base_type_round_trip_preserves_non_english_members()
    {
        // Arrange
        var original = CreateNonEnglishSpecimen();
        var options = ProductionOptions();

        // Act
        var json = JsonSerializer.Serialize<TitleCasingRulesDocument>(original, options);
        var roundTrip = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, options);

        // Assert
        AssertNonEnglishFullyPreserved(roundTrip, original);
        json.Should().Contain("ignoredSubjects");
        json.Should().Contain("knownTerms");
        json.Should().Contain("lowerCaseTerms");
    }

    [Fact(DisplayName =
        "INTEGRITY TitleCasingRules JSON (production options): when serializing English and Universal as TitleCasingRulesDocument, then knownTerms and lowerCaseTerms round-trip and ignoredSubjects is never emitted, because English/Universal must not gain NonEnglish-only members.")]
    public void production_options_english_and_universal_round_trip_without_ignored_subjects()
    {
        // Arrange
        var english = CreateEnglishSpecimen();
        var universal = CreateUniversalSpecimen();
        var options = ProductionOptions();

        // Act
        var englishJson = JsonSerializer.Serialize<TitleCasingRulesDocument>(english, options);
        var universalJson = JsonSerializer.Serialize<TitleCasingRulesDocument>(universal, options);
        var englishRoundTrip = JsonSerializer.Deserialize<TitleCasingRulesDocument>(englishJson, options);
        var universalRoundTrip = JsonSerializer.Deserialize<TitleCasingRulesDocument>(universalJson, options);

        // Assert
        AssertEnglishFullyPreserved(englishRoundTrip, english);
        AssertUniversalFullyPreserved(universalRoundTrip, universal);
        englishJson.Should().NotContain("ignoredSubjects");
        universalJson.Should().NotContain("ignoredSubjects");
        universalJson.Should().NotContain("lowerCaseTerms");
    }

    [Fact(DisplayName =
        "INTEGRITY TitleCasingRules JSON (FileRepository): when Write then Read via production options with WriteIndented, then polymorphic documents keep all members, because local file I/O shares JsonSerializerOptionsProvider with Cosmos.")]
    public async Task file_repository_round_trip_preserves_all_concrete_members()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "titlecasing-ser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var repo = new FileRepository(
            OptionsProvider,
            root,
            useEntityFolder: false,
            NullLogger<IFileRepository>.Instance);
        var nonEnglish = CreateNonEnglishSpecimen();
        var english = CreateEnglishSpecimen();
        var universal = CreateUniversalSpecimen();

        try
        {
            // Act
            await repo.Write(nonEnglish);
            await repo.Write(english);
            await repo.Write(universal);
            var nonEnglishRead = await repo.Read<TitleCasingRulesDocument>(nonEnglish.FileKey);
            var englishRead = await repo.Read<TitleCasingRulesDocument>(english.FileKey);
            var universalRead = await repo.Read<TitleCasingRulesDocument>(universal.FileKey);

            // Assert
            AssertNonEnglishFullyPreserved(nonEnglishRead, nonEnglish);
            AssertEnglishFullyPreserved(englishRead, english);
            AssertUniversalFullyPreserved(universalRead, universal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact(DisplayName =
        "INTEGRITY TitleCasingRules JSON (CosmosSystemTextJsonSerializer): when ToStream then FromStream as TitleCasingRulesDocument, then NonEnglish members survive, because the Cosmos STJ adapter must round-trip via concrete GetType() write and base-type read.")]
    public void cosmos_system_text_json_serializer_round_trip_preserves_non_english_members()
    {
        // Arrange
        var original = CreateNonEnglishSpecimen();
        var serializer = new CosmosSystemTextJsonSerializer(ProductionOptions());

        // Act
        using var stream = serializer.ToStream(original);
        var roundTrip = serializer.FromStream<TitleCasingRulesDocument>(stream);

        // Assert
        AssertNonEnglishFullyPreserved(roundTrip, original);
    }

    [Fact(DisplayName =
        "INTEGRITY TitleCasingRules JSON (Cosmos downloader shape): when Serialize uses typeof(TitleCasingRulesDocument) then Deserialize, then all concrete members survive, because CosmosDbDownloader WriteJson(runtimeType) serializes through the base converter.")]
    public void cosmos_downloader_runtime_type_round_trip_preserves_all_members()
    {
        // Arrange
        var nonEnglish = CreateNonEnglishSpecimen();
        var english = CreateEnglishSpecimen();
        var universal = CreateUniversalSpecimen();
        var options = ProductionOptions();
        var runtimeType = typeof(TitleCasingRulesDocument);

        // Act
        var nonEnglishJson = JsonSerializer.Serialize(nonEnglish, runtimeType, options);
        var englishJson = JsonSerializer.Serialize(english, runtimeType, options);
        var universalJson = JsonSerializer.Serialize(universal, runtimeType, options);
        var nonEnglishRoundTrip =
            JsonSerializer.Deserialize(nonEnglishJson, runtimeType, options) as TitleCasingRulesDocument;
        var englishRoundTrip =
            JsonSerializer.Deserialize(englishJson, runtimeType, options) as TitleCasingRulesDocument;
        var universalRoundTrip =
            JsonSerializer.Deserialize(universalJson, runtimeType, options) as TitleCasingRulesDocument;

        // Assert
        AssertNonEnglishFullyPreserved(nonEnglishRoundTrip, nonEnglish);
        AssertEnglishFullyPreserved(englishRoundTrip, english);
        AssertUniversalFullyPreserved(universalRoundTrip, universal);
    }

    private static NonEnglishTitleCasingRulesDocument CreateNonEnglishSpecimen() =>
        new("es")
        {
            LowerCaseTerms = ["de", "la"],
            KnownTerms =
            [
                new KnownTermEntry { Literal = "TermOne", Pattern = @"\bTermOne\b" },
                new KnownTermEntry { Literal = "Term Two", Pattern = @"\bTerm Two\b", Options = "IgnoreCase" }
            ],
            IgnoredSubjects = ["SubjectAlpha", "SubjectBeta"]
        };

    private static EnglishTitleCasingRulesDocument CreateEnglishSpecimen() =>
        new()
        {
            LowerCaseTerms = ["of", "the"],
            KnownTerms =
            [
                new KnownTermEntry { Literal = "EnTerm", Pattern = @"\bEnTerm\b" }
            ]
        };

    private static UniversalTitleCasingRulesDocument CreateUniversalSpecimen() =>
        new()
        {
            KnownTerms =
            [
                new KnownTermEntry { Literal = "UniTerm", Pattern = @"\bUniTerm\b" }
            ]
        };

    private static void AssertNonEnglishFullyPreserved(
        TitleCasingRulesDocument? actual,
        NonEnglishTitleCasingRulesDocument expected)
    {
        var nonEnglish = actual.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.Language.Should().Be(expected.Language);
        nonEnglish.Id.Should().Be(expected.Id);
        nonEnglish.LowerCaseTerms.Should().Equal(expected.LowerCaseTerms);
        nonEnglish.IgnoredSubjects.Should().Equal(expected.IgnoredSubjects);
        AssertKnownTermsEqual(nonEnglish.KnownTerms, expected.KnownTerms);
    }

    private static void AssertEnglishFullyPreserved(
        TitleCasingRulesDocument? actual,
        EnglishTitleCasingRulesDocument expected)
    {
        var english = actual.Should().BeOfType<EnglishTitleCasingRulesDocument>().Subject;
        english.Language.Should().Be("en");
        english.Id.Should().Be(expected.Id);
        english.LowerCaseTerms.Should().Equal(expected.LowerCaseTerms);
        AssertKnownTermsEqual(english.KnownTerms, expected.KnownTerms);
    }

    private static void AssertUniversalFullyPreserved(
        TitleCasingRulesDocument? actual,
        UniversalTitleCasingRulesDocument expected)
    {
        var universal = actual.Should().BeOfType<UniversalTitleCasingRulesDocument>().Subject;
        universal.Language.Should().Be(TitleCasingRulesDocument.UniversalLanguageKey);
        universal.Id.Should().Be(expected.Id);
        AssertKnownTermsEqual(universal.KnownTerms, expected.KnownTerms);
    }

    private static void AssertKnownTermsEqual(List<KnownTermEntry> actual, List<KnownTermEntry> expected)
    {
        actual.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            actual[i].Literal.Should().Be(expected[i].Literal);
            actual[i].Pattern.Should().Be(expected[i].Pattern);
            actual[i].Options.Should().Be(expected[i].Options);
        }
    }
}
