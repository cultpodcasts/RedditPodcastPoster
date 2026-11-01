using FluentAssertions;
using RedditPodcastPoster.UrlSubmission.Languages;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Persian and Urdu share the Arabic script. A few letters that Arabic does not use decide which
/// language it is, and the shared letters then count towards that language.
/// </summary>
public class CatalogueLanguageScriptRules
{
    private readonly CatalogueLanguageDetector _detector = new();

    [Fact(DisplayName =
        "When a title is written in Arabic script and uses Persian letters, detection names Persian, because those letters are not Arabic and Persian is a recognised language.")]
    public void persian_letters_name_persian()
    {
        // Arrange
        var texts = CatalogueLanguageTexts.From(
            parentTitle: "\u06AF\u0641\u062A\u06AF\u0648\u06CC \u0641\u0627\u0631\u0633\u06CC",
            parentDescription: null,
            childTitle: "\u067E\u06CC\u0627\u0645\u06CC \u062F\u0631\u0628\u0627\u0631\u0647 \u0641\u0631\u0647\u0646\u06AF \u0648 \u062A\u0627\u0631\u06CC\u062E",
            childDescription: "\u0627\u06CC\u0646 \u06CC\u06A9 \u06AF\u0641\u062A\u06AF\u0648\u06CC \u0641\u0627\u0631\u0633\u06CC \u062F\u0631\u0628\u0627\u0631\u0647 \u067E\u06CC\u0627\u0645 \u0648 \u0641\u0631\u0647\u0646\u06AF \u0627\u0633\u062A",
            parentNoun: "Podcast",
            childNoun: "Episode");

        // Act
        var detection = _detector.Detect(texts);

        // Assert
        detection.Code.Should().Be("fa");
        detection.EnglishName.Should().Be("Persian");
    }

    [Fact(DisplayName =
        "When Arabic-script text uses only letters Arabic itself uses, detection names Arabic, because nothing marks it as Persian or Urdu.")]
    public void shared_arabic_script_stays_arabic()
    {
        // Arrange
        var texts = CatalogueLanguageTexts.From(
            parentTitle: null,
            parentDescription: null,
            childTitle: "\u0647\u0630\u0627 \u0628\u0631\u0646\u0627\u0645\u062C \u0639\u0646 \u0627\u0644\u062A\u0627\u0631\u064A\u062E",
            childDescription: "\u0648\u0627\u0644\u062B\u0642\u0627\u0641\u0629 \u0641\u064A \u0627\u0644\u0639\u0627\u0644\u0645 \u0627\u0644\u0639\u0631\u0628\u064A \u0645\u0639 \u0642\u0635\u0629 \u0637\u0648\u064A\u0644\u0629",
            parentNoun: "Podcast",
            childNoun: "Episode");

        // Act
        var detection = _detector.Detect(texts);

        // Assert
        detection.Code.Should().Be("ar");
        detection.EnglishName.Should().Be("Arabic");
    }

    [Fact(DisplayName =
        "When Arabic-script text includes an Urdu-only letter, detection names Urdu, because that letter is not used for Persian.")]
    public void urdu_only_letter_names_urdu()
    {
        // Arrange
        var texts = CatalogueLanguageTexts.From(
            parentTitle: null,
            parentDescription: null,
            childTitle: "\u06CC\u06C1 \u0627\u0631\u062F\u0648 \u0679\u06CC\u06A9\u0633\u0679 \u06C1\u06D2",
            childDescription: "\u0627\u0648\u0631 \u067E\u0631 \u0686\u0644\u062A\u0627 \u06C1\u06D2 \u0627\u0633 \u0642\u0635\u06D2 \u0645\u06CC\u06BA",
            parentNoun: "Podcast",
            childNoun: "Episode");

        // Act
        var detection = _detector.Detect(texts);

        // Assert
        detection.Code.Should().Be("ur");
        detection.EnglishName.Should().Be("Urdu");
    }
}
