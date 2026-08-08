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
        "Supported language admin: an unknown language name does not resolve, because only .NET neutral culture English names are valid.")]
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
}
