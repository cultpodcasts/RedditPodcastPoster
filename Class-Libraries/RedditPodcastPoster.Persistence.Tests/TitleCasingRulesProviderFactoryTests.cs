using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Lookups;
using RedditPodcastPoster.Text.TitleCasing;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace RedditPodcastPoster.Persistence.Tests;

public class TitleCasingRulesProviderFactoryTests
{
    [Fact(DisplayName =
        "Title-casing load: when universal and English exist, then both are point-read in parallel and GetAll is not called, because the hot path is * + en.")]
    public async Task Create_when_universal_and_english_exist_does_not_call_GetAll()
    {
        // Arrange
        var universal = new LanguageTitleCasingRulesDocument(LanguageTitleCasingRulesDocument.UniversalLanguageKey)
        {
            KnownTerms = [new KnownTermEntry { Literal = "BBC", Pattern = "BBC" }]
        };
        var english = new LanguageTitleCasingRulesDocument("en")
        {
            LowerCaseTerms = ["of", "the"],
            KnownTerms = []
        };
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        titleRepo.Setup(x => x.Get(LanguageTitleCasingRulesDocument.UniversalLanguageKey))
            .ReturnsAsync(universal);
        titleRepo.Setup(x => x.Get("en")).ReturnsAsync(english);
        var lookup = new Mock<ILookupRepository>();
        var sut = new TitleCasingRulesProviderFactory(
            titleRepo.Object,
            lookup.Object,
            NullLogger<TitleCasingRulesProviderFactory>.Instance);

        // Act
        var provider = await sut.Create();

        // Assert
        provider.GetAll().Keys.Should().BeEquivalentTo(
            LanguageTitleCasingRulesDocument.UniversalLanguageKey,
            "en");
        provider.GetUniversalKnownTerms().Should().ContainSingle(t => t.Literal == "BBC");
        provider.GetLowerCaseExpressions("en").Keys.Should().Contain("of");
        titleRepo.Verify(x => x.GetAll(), Times.Never);
        lookup.Verify(x => x.GetKnownTerms<KnownTermsModel>(), Times.Never);
    }

    [Fact(DisplayName =
        "Title-casing load: when English is missing, then GetAll loads other languages excluding universal and English is seeded, because * was already fetched.")]
    public async Task Create_when_english_missing_GetAll_skips_universal_and_seeds_en()
    {
        // Arrange
        var universal = new LanguageTitleCasingRulesDocument(LanguageTitleCasingRulesDocument.UniversalLanguageKey)
        {
            KnownTerms = [new KnownTermEntry { Literal = "UN", Pattern = "UN" }]
        };
        var french = new LanguageTitleCasingRulesDocument("fr")
        {
            LowerCaseTerms = ["de", "la"],
            KnownTerms = []
        };
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        titleRepo.Setup(x => x.Get(LanguageTitleCasingRulesDocument.UniversalLanguageKey))
            .ReturnsAsync(universal);
        titleRepo.Setup(x => x.Get("en")).ReturnsAsync((LanguageTitleCasingRulesDocument?)null);
        titleRepo.Setup(x => x.GetAll()).Returns(Documents(universal, french));
        var lookup = new Mock<ILookupRepository>();
        lookup.Setup(x => x.GetKnownTerms<KnownTermsModel>())
            .ReturnsAsync((KnownTermsModel?)null);
        var sut = new TitleCasingRulesProviderFactory(
            titleRepo.Object,
            lookup.Object,
            NullLogger<TitleCasingRulesProviderFactory>.Instance);

        // Act
        var provider = await sut.Create();

        // Assert
        provider.GetAll().Keys.Should().BeEquivalentTo(
            LanguageTitleCasingRulesDocument.UniversalLanguageKey,
            "fr",
            "en");
        provider.GetUniversalKnownTerms().Should().ContainSingle(t => t.Literal == "UN");
        provider.GetLowerCaseExpressions("fr").Keys.Should().Contain("de");
        provider.GetLowerCaseExpressions("en").Should().NotBeEmpty();
        titleRepo.Verify(x => x.GetAll(), Times.Once);
        titleRepo.Verify(x => x.Get(LanguageTitleCasingRulesDocument.UniversalLanguageKey), Times.Once);
    }

    [Fact(DisplayName =
        "Title-casing load: when no English document exists and GetAll is empty, then English defaults are seeded in memory.")]
    public async Task Create_when_english_missing_and_GetAll_empty_seeds_english_defaults()
    {
        // Arrange
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        titleRepo.Setup(x => x.Get(It.IsAny<string>()))
            .ReturnsAsync((LanguageTitleCasingRulesDocument?)null);
        titleRepo.Setup(x => x.GetAll()).Returns(EmptyDocuments());
        var lookup = new Mock<ILookupRepository>();
        lookup.Setup(x => x.GetKnownTerms<KnownTermsModel>())
            .ReturnsAsync((KnownTermsModel?)null);
        var sut = new TitleCasingRulesProviderFactory(
            titleRepo.Object,
            lookup.Object,
            NullLogger<TitleCasingRulesProviderFactory>.Instance);

        // Act
        var provider = await sut.Create();

        // Assert
        provider.GetAll().Should().ContainKey("en");
        provider.GetLowerCaseExpressions("en").Should().NotBeEmpty();
        titleRepo.Verify(x => x.Get(LanguageTitleCasingRulesDocument.UniversalLanguageKey), Times.Once);
        titleRepo.Verify(x => x.Get("en"), Times.Once);
        titleRepo.Verify(x => x.GetAll(), Times.Once);
    }

    private static async IAsyncEnumerable<LanguageTitleCasingRulesDocument> EmptyDocuments()
    {
        yield break;
    }

    private static async IAsyncEnumerable<LanguageTitleCasingRulesDocument> Documents(
        params LanguageTitleCasingRulesDocument[] documents)
    {
        foreach (var document in documents)
        {
            yield return document;
        }

        await Task.CompletedTask;
    }
}
