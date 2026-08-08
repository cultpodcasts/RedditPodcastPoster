using Api.Services.TitleCasingRules;
using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;
using Xunit;

namespace FunctionHost.Tests.BusinessRules.TitleCasingRules;

public class TitleCasingRulesMutationRulesTests
{
    [Fact(DisplayName =
        "Title-casing admin POST lower-case term: appending a new term keeps existing terms and sorts case-insensitively, because deltas must not wipe siblings.")]
    public void add_lower_case_term_appends_and_orders()
    {
        // Arrange
        var existing = new[] { "of", "the" };

        // Act
        var result = TitleCasingRulesMutationRules.TryAddLowerCaseTerm(existing, "and");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Terms.Should().Equal("and", "of", "the");
    }

    [Fact(DisplayName =
        "Title-casing admin POST lower-case term: adding a duplicate (case-insensitive) is idempotent, because the list must stay unique.")]
    public void add_duplicate_lower_case_term_is_idempotent()
    {
        // Arrange
        var existing = new[] { "The", "of" };

        // Act
        var result = TitleCasingRulesMutationRules.TryAddLowerCaseTerm(existing, "the");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Terms.Should().Equal("of", "The");
    }

    [Fact(DisplayName =
        "Title-casing admin POST lower-case term: an empty term fails, because Add requires a non-empty word.")]
    public void add_empty_lower_case_term_fails()
    {
        // Arrange
        var existing = new[] { "the" };

        // Act
        var result = TitleCasingRulesMutationRules.TryAddLowerCaseTerm(existing, "   ");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("required");
    }

    [Fact(DisplayName =
        "Title-casing admin DELETE lower-case term: removing by value drops that term and keeps the rest, because deletes are targeted deltas.")]
    public void delete_lower_case_term_removes_match()
    {
        // Arrange
        var existing = new[] { "and", "of", "the" };

        // Act
        var result = TitleCasingRulesMutationRules.TryRemoveLowerCaseTerm(existing, "OF");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Terms.Should().Equal("and", "the");
    }

    [Fact(DisplayName =
        "Title-casing admin DELETE lower-case term: removing an unknown term fails, because only registered terms can be deleted.")]
    public void delete_unknown_lower_case_term_fails()
    {
        // Arrange
        var existing = new[] { "the" };

        // Act
        var result = TitleCasingRulesMutationRules.TryRemoveLowerCaseTerm(existing, "missing");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("missing");
        result.Error.Should().Contain("not in the list");
    }

    [Fact(DisplayName =
        "Title-casing admin POST known term: adding a new literal appends it without removing other known terms, because deltas must preserve siblings.")]
    public void add_known_term_appends()
    {
        // Arrange
        var existing = new[]
        {
            new KnownTermEntry { Literal = "BBC", Pattern = @"\bBBC\b", Options = "IgnoreCase, Compiled" }
        };

        // Act
        var result = TitleCasingRulesMutationRules.TryUpsertKnownTerm(
            existing,
            "NASA",
            @"\bNASA\b",
            "IgnoreCase, Compiled");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Terms.Should().HaveCount(2);
        result.Terms.Should().Contain(t => t.Literal == "BBC");
        result.Terms.Should().Contain(t => t.Literal == "NASA" && t.Pattern == @"\bNASA\b");
    }

    [Fact(DisplayName =
        "Title-casing admin POST known term: posting an existing literal (case-insensitive) replaces pattern/options, because literal is the stable key for upsert/edit.")]
    public void upsert_known_term_replaces_by_literal()
    {
        // Arrange
        var existing = new[]
        {
            new KnownTermEntry { Literal = "Bbc", Pattern = @"\bold\b", Options = "IgnoreCase, Compiled" },
            new KnownTermEntry { Literal = "NASA", Pattern = @"\bNASA\b", Options = "IgnoreCase, Compiled" }
        };

        // Act
        var result = TitleCasingRulesMutationRules.TryUpsertKnownTerm(
            existing,
            "BBC",
            @"\bBBC\b",
            "IgnoreCase, Compiled");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Terms.Should().HaveCount(2);
        result.Terms.Should().ContainSingle(t => t.Literal == "BBC" && t.Pattern == @"\bBBC\b");
        result.Terms.Should().Contain(t => t.Literal == "NASA");
    }

    [Fact(DisplayName =
        "Title-casing admin POST known term: an invalid regex pattern fails, because known terms must compile before save.")]
    public void add_known_term_with_invalid_pattern_fails()
    {
        // Arrange
        var existing = Array.Empty<KnownTermEntry>();

        // Act
        var result = TitleCasingRulesMutationRules.TryUpsertKnownTerm(
            existing,
            "Broken",
            "(",
            "IgnoreCase, Compiled");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid regex pattern");
        result.Error.Should().Contain("Broken");
    }

    [Fact(DisplayName =
        "Title-casing admin DELETE known term: removing by literal drops that entry and keeps siblings, because literal is the delete key.")]
    public void delete_known_term_by_literal()
    {
        // Arrange
        var existing = new[]
        {
            new KnownTermEntry { Literal = "BBC", Pattern = @"\bBBC\b", Options = "IgnoreCase, Compiled" },
            new KnownTermEntry { Literal = "NASA", Pattern = @"\bNASA\b", Options = "IgnoreCase, Compiled" }
        };

        // Act
        var result = TitleCasingRulesMutationRules.TryRemoveKnownTerm(existing, "bbc");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Terms.Should().ContainSingle().Which.Literal.Should().Be("NASA");
    }

    [Fact(DisplayName =
        "Title-casing admin DELETE known term: removing an unknown literal fails, because only registered known terms can be deleted.")]
    public void delete_unknown_known_term_fails()
    {
        // Arrange
        var existing = new[]
        {
            new KnownTermEntry { Literal = "BBC", Pattern = @"\bBBC\b", Options = "IgnoreCase, Compiled" }
        };

        // Act
        var result = TitleCasingRulesMutationRules.TryRemoveKnownTerm(existing, "NASA");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("NASA");
        result.Error.Should().Contain("not in the list");
    }
}
