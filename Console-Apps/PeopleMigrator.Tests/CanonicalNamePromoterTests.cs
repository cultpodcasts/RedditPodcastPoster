using FluentAssertions;
using Xunit;

namespace PeopleMigrator.Tests;

public class CanonicalNamePromoterTests
{
    [Fact]
    public void Promote_swaps_first_name_only_canonical_for_full_name_alias()
    {
        var result = CanonicalNamePromoter.Promote(
            "Chloe",
            ["Chloe Carmichael"]);

        result.WasPromoted.Should().BeTrue();
        result.CanonicalName.Should().Be("Chloe Carmichael");
        result.Aliases.Should().Equal("Chloe");
    }

    [Fact]
    public void Promote_promotes_cleaner_alias_over_congressional_title_canonical()
    {
        var result = CanonicalNamePromoter.Promote(
            "Congresswoman Madeleine Dean",
            ["Madeleine Dean"]);

        result.WasPromoted.Should().BeTrue();
        result.CanonicalName.Should().Be("Madeleine Dean");
        result.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Promote_strips_title_when_no_cleaner_alias_exists()
    {
        var result = CanonicalNamePromoter.Promote(
            "Congressman Jared Moskowitz",
            []);

        result.WasPromoted.Should().BeTrue();
        result.CanonicalName.Should().Be("Jared Moskowitz");
        result.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Promote_keeps_nickname_alias_for_alexandra_stein()
    {
        var result = CanonicalNamePromoter.Promote(
            "Alexandra Stein",
            ["Alex Stein"]);

        result.WasPromoted.Should().BeFalse();
        result.CanonicalName.Should().Be("Alexandra Stein");
        result.Aliases.Should().Equal("Alex Stein");
    }

    [Fact]
    public void Promote_does_not_promote_noisy_title_alias()
    {
        var result = CanonicalNamePromoter.Promote(
            "Senator Sheldon Whitehouse",
            ["Democratic Senator Sheldon Whitehouse"]);

        result.WasPromoted.Should().BeTrue();
        result.CanonicalName.Should().Be("Sheldon Whitehouse");
        result.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Promote_prefers_clean_alias_over_state_senator_title()
    {
        var result = CanonicalNamePromoter.Promote(
            "NYS Senator Zellnor Y. Myrie 维",
            ["Zellnor Myrie"]);

        result.WasPromoted.Should().BeTrue();
        result.CanonicalName.Should().Be("Zellnor Myrie");
        result.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Promote_strips_dr_prefix_from_canonical()
    {
        var result = CanonicalNamePromoter.Promote(
            "Dr. Cynthia Miller-Idriss",
            []);

        result.WasPromoted.Should().BeTrue();
        result.CanonicalName.Should().Be("Cynthia Miller-Idriss");
    }

    [Fact]
    public void SwapCanonicalWithAlias_promotes_alias_and_demotes_old_canonical()
    {
        var result = CanonicalNamePromoter.SwapCanonicalWithAlias(
            "Chloe",
            ["Chloe Carmichael", "Dr Chloe Carmichael"],
            "Chloe Carmichael");

        result.CanonicalName.Should().Be("Chloe Carmichael");
        result.Aliases.Should().Equal("Chloe");
    }

    [Fact]
    public void SwapCanonicalWithAlias_filters_would_match_canonical_duplicates()
    {
        var result = CanonicalNamePromoter.SwapCanonicalWithAlias(
            "Congresswoman Madeleine Dean",
            ["Madeleine Dean", "Rep Madeleine Dean"],
            "Madeleine Dean");

        result.CanonicalName.Should().Be("Madeleine Dean");
        result.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void SwapCanonicalWithAlias_strips_credential_suffix_from_old_canonical()
    {
        var result = CanonicalNamePromoter.SwapCanonicalWithAlias(
            "Daniel Shaw LCSW",
            ["Daniel Shaw"],
            "Daniel Shaw");

        result.CanonicalName.Should().Be("Daniel Shaw");
        result.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Promote_strips_parenthetical_social_handle_from_canonical()
    {
        var result = CanonicalNamePromoter.Promote(
            "Eliza Anyangwe (elizatalks.bsky.social)",
            ["Eliza Anyangwe"]);

        result.WasPromoted.Should().BeTrue();
        result.CanonicalName.Should().Be("Eliza Anyangwe");
        result.Aliases.Should().BeEmpty();
    }
}
