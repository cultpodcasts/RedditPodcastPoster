using Api.Models;
using Api.Services.TitleCasingRules;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class TitleCasingRulesUpdateServiceTests
{
    [Fact(DisplayName =
        "PUT for universal language '*' forces empty lower-case terms even when the body sends some.")]
    public async Task UpdateAsync_WithUniversalAndLowerCaseTerms_ClearsLowerCaseTerms()
    {
        // Arrange
        LanguageTitleCasingRulesDocument? saved = null;
        var repo = new Mock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Save(It.IsAny<LanguageTitleCasingRulesDocument>()))
            .Callback<LanguageTitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var sut = new TitleCasingRulesUpdateService(
            repo.Object,
            NullLogger<TitleCasingRulesUpdateService>.Instance);
        var body = new LanguageTitleCasingRulesUpdateRequest
        {
            LowerCaseTerms = ["the", "of"],
            KnownTerms =
            [
                new KnownTermUpdate
                {
                    Literal = "BBC",
                    Pattern = @"\bBBC\b",
                    Options = "IgnoreCase, Compiled"
                }
            ]
        };

        // Act
        var result = await sut.UpdateAsync(
            LanguageTitleCasingRulesDocument.UniversalLanguageKey,
            body,
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        saved.Should().NotBeNull();
        saved!.Language.Should().Be(LanguageTitleCasingRulesDocument.UniversalLanguageKey);
        saved.LowerCaseTerms.Should().BeEmpty();
        saved.KnownTerms.Should().ContainSingle().Which.Literal.Should().Be("BBC");
    }
}
