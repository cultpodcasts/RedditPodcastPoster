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
        "Supported language admin: ListAll includes Kiswahili so the website Add control can resolve the same spelling as Save.")]
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

public class SupportedLanguagesUpdateRulesTests
{
    [Fact(DisplayName =
        "Supported language admin PUT: adding a language by a known name stores the derived code and canonical English name, because the user cannot set the code.")]
    public void add_by_known_name_derives_code()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: null, Name: "Dutch")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().ContainSingle();
        result.Languages[0].Code.Should().Be("nl");
        result.Languages[0].Name.Should().Be("Dutch");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: adding a language by an unknown name fails, because the name must exist in .NET culture data.")]
    public void add_by_unknown_name_fails()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: null, Name: "Klingon Cult Dialect")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Unknown language name");
        result.Error.Should().Contain("Klingon Cult Dialect");
        result.Languages.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Supported language admin PUT: a client-supplied code that does not match the name-derived code is rejected, because language codes cannot be set or edited by the user.")]
    public void mismatched_client_code_is_rejected()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: "xx", Name: "German")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("derived as 'de'");
        result.Error.Should().Contain("xx");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: renaming in place by keeping an existing code while changing the name to another language is rejected, because existing languages cannot be edited.")]
    public void in_place_rename_keeping_old_code_is_rejected()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: "en", Name: "French")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("derived as 'fr'");
        result.Error.Should().Contain("en");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: omitting a previously stored language is allowed, because users may delete languages.")]
    public void omitting_a_language_is_allowed_as_delete()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: "en", Name: "English")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().ContainSingle();
        result.Languages[0].Code.Should().Be("en");
        result.Languages[0].Name.Should().Be("English");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: Filipino may be added with derived code 'fil', because code length must not be limited to two characters.")]
    public void filipino_add_allows_three_letter_derived_code()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: "", Name: "Filipino")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().ContainSingle();
        result.Languages[0].Code.Should().Be("fil");
        result.Languages[0].Code.Length.Should().BeGreaterThan(2);
        result.Languages[0].Name.Should().Be("Filipino");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: an empty languages list fails, because at least one supported language is required.")]
    public void empty_proposed_list_fails()
    {
        // Arrange
        var proposed = Array.Empty<SupportedLanguageProposal>();

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("at least one entry");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: a matching client-supplied code is accepted and the canonical culture English name is stored, because the server remains authoritative for code derivation.")]
    public void matching_client_code_is_accepted_with_canonical_name()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: "IT", Name: "italian")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().ContainSingle();
        result.Languages[0].Code.Should().Be("it");
        result.Languages[0].Name.Should().Be("Italian");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: putting Kiswahili with code sw succeeds and keeps Kiswahili, because the register must not drop or rename that existing language.")]
    public void kiswahili_existing_row_survives_put()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: "sw", Name: "Kiswahili"),
            new SupportedLanguageProposal(Code: "en", Name: "English")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Languages.Should().HaveCount(2);
        result.Languages.Should().Contain(l => l.Code == "sw" && l.Name == "Kiswahili");
        result.Languages.Should().Contain(l => l.Code == "en" && l.Name == "English");
    }

    [Fact(DisplayName =
        "Supported language admin PUT: unknown new names are all reported when mixed with valid rows, because Save must not fail only on the first invalid while silently ignoring others.")]
    public void multiple_unknown_names_are_listed()
    {
        // Arrange
        var proposed = new[]
        {
            new SupportedLanguageProposal(Code: "en", Name: "English"),
            new SupportedLanguageProposal(Code: "", Name: "xyz"),
            new SupportedLanguageProposal(Code: "", Name: "Klingon Cult Dialect")
        };

        // Act
        var result = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("xyz");
        result.Error.Should().Contain("Klingon Cult Dialect");
    }
}
