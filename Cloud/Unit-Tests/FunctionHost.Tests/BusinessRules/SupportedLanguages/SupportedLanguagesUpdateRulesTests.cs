using Api.Services.SupportedLanguages;
using FluentAssertions;
using RedditPodcastPoster.Models.Languages;
using Xunit;

namespace FunctionHost.Tests.BusinessRules.SupportedLanguages;

public class NeutralCultureLanguageLookupRules
{
    [Fact(DisplayName =
        "Supported language admin: a known .NET neutral culture English name resolves to its ISO language code, because codes are derived from culture data not user input.")]
    public void known_english_name_resolves_to_iso_code()
    {
        // Arrange
        const string languageName = "French";

        // Act
        var resolved = NeutralCultureLanguageLookup.TryResolveByEnglishName(
            languageName,
            out var code,
            out var canonicalName);

        // Assert
        resolved.Should().BeTrue();
        code.Should().Be("fr");
        canonicalName.Should().Be("French");
    }

    [Fact(DisplayName =
        "Supported language admin: Filipino resolves to code 'fil' (longer than 2 characters), because ISO 639 has no two-letter code and length must not be constrained to 2.")]
    public void filipino_resolves_to_three_letter_code()
    {
        // Arrange
        const string languageName = "Filipino";

        // Act
        var resolved = NeutralCultureLanguageLookup.TryResolveByEnglishName(
            languageName,
            out var code,
            out var canonicalName);

        // Assert
        resolved.Should().BeTrue();
        code.Should().Be("fil");
        code.Length.Should().BeGreaterThan(2);
        canonicalName.Should().Be("Filipino");
    }

    [Fact(DisplayName =
        "Supported language admin: an unknown language name does not resolve, because only .NET neutral culture names (and known aliases) are valid.")]
    public void unknown_english_name_does_not_resolve()
    {
        // Arrange
        const string languageName = "Not A Real Culture Language";

        // Act
        var resolved = NeutralCultureLanguageLookup.TryResolveByEnglishName(
            languageName,
            out var code,
            out var canonicalName);

        // Assert
        resolved.Should().BeFalse();
        code.Should().BeEmpty();
        canonicalName.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Supported language admin: culture name matching is case-insensitive, because admins may type a valid English name with different casing.")]
    public void english_name_matching_is_case_insensitive()
    {
        // Arrange
        const string languageName = "sPaNiSh";

        // Act
        var resolved = NeutralCultureLanguageLookup.TryResolveByEnglishName(
            languageName,
            out var code,
            out var canonicalName);

        // Assert
        resolved.Should().BeTrue();
        code.Should().Be("es");
        canonicalName.Should().Be("Spanish");
    }

    [Fact(DisplayName =
        "Supported language admin: Kiswahili resolves to code 'sw' and keeps the Kiswahili spelling, because registered/R2 data uses that name and ICU hosts may expose EnglishName as Swahili.")]
    public void kiswahili_resolves_without_dropping_registered_spelling()
    {
        // Arrange
        const string languageName = "Kiswahili";

        // Act
        var resolved = NeutralCultureLanguageLookup.TryResolveByEnglishName(
            languageName,
            out var code,
            out var canonicalName);

        // Assert
        resolved.Should().BeTrue();
        code.Should().Be("sw");
        canonicalName.Should().Be("Kiswahili");
    }

    [Fact(DisplayName =
        "Supported language admin: ASCII 'maori' resolves to Māori / mi, because diacritic-insensitive matching is required for Add UX.")]
    public void maori_without_macron_resolves()
    {
        // Arrange
        const string languageName = "maori";

        // Act
        var resolved = NeutralCultureLanguageLookup.TryResolveByEnglishName(
            languageName,
            out var code,
            out var canonicalName);

        // Assert
        resolved.Should().BeTrue();
        code.Should().Be("mi");
        canonicalName.Should().Be("Māori");
    }

    [Fact(DisplayName =
        "Supported language admin: ListAll includes Kiswahili so the website Add control can resolve the same spelling as the Add API.")]
    public void list_all_includes_kiswahili_alias_spelling()
    {
        // Arrange / Act
        var cultures = NeutralCultureLanguageLookup.ListAll();

        // Assert
        cultures.Should().Contain(c =>
            c.Code.Equals("sw", StringComparison.OrdinalIgnoreCase) &&
            c.Name.Equals("Kiswahili", StringComparison.OrdinalIgnoreCase));
    }
}

public class SupportedLanguagesMutationRulesTests
{
    [Fact(DisplayName =
        "Supported language admin POST: adding by a known name appends the derived code and canonical English name, because the client only sends the name.")]
    public void add_by_known_name_appends_derived_code()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryAdd(existing, "Dutch");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().HaveCount(2);
        result.Languages.Should().Contain(l => l.Code == "nl" && l.Name == "Dutch");
        result.Languages.Should().Contain(l => l.Code == "en" && l.Name == "English");
    }

    [Fact(DisplayName =
        "Supported language admin POST: adding an unknown name fails, because the name must exist in .NET culture data.")]
    public void add_by_unknown_name_fails()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryAdd(existing, "Klingon Cult Dialect");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Unknown language name");
        result.Error.Should().Contain("Klingon Cult Dialect");
    }

    [Fact(DisplayName =
        "Supported language admin POST: adding a language already present is idempotent and keeps the existing list, because duplicates are not stored.")]
    public void add_existing_language_is_idempotent()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" },
            new SupportedLanguage { Code = "nl", Name = "Dutch" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryAdd(existing, "dutch");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().HaveCount(2);
        result.Languages.Should().Contain(l => l.Code == "nl" && l.Name == "Dutch");
    }

    [Fact(DisplayName =
        "Supported language admin POST: an empty name fails, because Add requires a culture English or native name.")]
    public void add_empty_name_fails()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryAdd(existing, "   ");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("required");
    }

    [Fact(DisplayName =
        "Supported language admin DELETE: removing by code drops that row and keeps the rest ordered by name, because deletes are targeted deltas.")]
    public void delete_by_code_removes_row()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" },
            new SupportedLanguage { Code = "nl", Name = "Dutch" },
            new SupportedLanguage { Code = "fr", Name = "French" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryRemove(existing, "nl");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().HaveCount(2);
        result.Languages.Should().NotContain(l => l.Code == "nl");
        result.Languages.Should().Contain(l => l.Code == "en");
        result.Languages.Should().Contain(l => l.Code == "fr");
    }

    [Fact(DisplayName =
        "Supported language admin DELETE: removing an unknown code fails, because only registered languages can be deleted.")]
    public void delete_unknown_code_fails()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryRemove(existing, "xx");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("xx");
        result.Error.Should().Contain("not in the supported list");
    }

    [Fact(DisplayName =
        "Supported language admin DELETE: removing the last remaining language fails, because at least one supported language is required.")]
    public void delete_last_language_fails()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryRemove(existing, "en");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("last supported language");
    }

    [Fact(DisplayName =
        "Supported language admin DELETE: code matching is case-insensitive, because culture codes are compared without regard to case.")]
    public void delete_code_matching_is_case_insensitive()
    {
        // Arrange
        var existing = new[]
        {
            new SupportedLanguage { Code = "en", Name = "English" },
            new SupportedLanguage { Code = "FR", Name = "French" }
        };

        // Act
        var result = SupportedLanguagesMutationRules.TryRemove(existing, "fr");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().ContainSingle();
        result.Languages[0].Code.Should().Be("en");
    }
}
