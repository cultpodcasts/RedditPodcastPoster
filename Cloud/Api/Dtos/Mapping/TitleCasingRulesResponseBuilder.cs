using Api.Dtos;
using RedditPodcastPoster.Models.TitleCasing;

namespace Api.Dtos.Mapping;

public static class TitleCasingRulesResponseBuilder
{
    public static LanguageTitleCasingRulesResponse Build(
        TitleCasingRulesDocument document,
        bool isDefault)
    {
        IReadOnlyList<string> lowerCaseTerms = document is LanguageTitleCasingRulesDocument language
            ? language.LowerCaseTerms
            : [];

        IReadOnlyList<string> ignoredSubjects = document is NonEnglishTitleCasingRulesDocument nonEnglish
            ? nonEnglish.IgnoredSubjects ?? []
            : [];

        return new()
        {
            Language = document.Language,
            IsDefault = isDefault,
            LowerCaseTerms = lowerCaseTerms,
            KnownTerms = document.KnownTerms
                .Select(t => new LanguageTitleCasingRulesResponse.KnownTermDto
                {
                    Literal = t.Literal,
                    Pattern = t.Pattern,
                    Options = t.Options
                })
                .ToList(),
            IgnoredSubjects = ignoredSubjects
        };
    }
}
