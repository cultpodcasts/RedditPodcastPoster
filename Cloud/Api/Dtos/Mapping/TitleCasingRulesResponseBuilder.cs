using Api.Dtos;
using RedditPodcastPoster.Models.TitleCasing;

namespace Api.Dtos.Mapping;

public static class TitleCasingRulesResponseBuilder
{
    public static LanguageTitleCasingRulesResponse Build(
        LanguageTitleCasingRulesDocument document,
        bool isDefault) =>
        new()
        {
            Language = document.Language,
            IsDefault = isDefault,
            LowerCaseTerms = document.LowerCaseTerms,
            KnownTerms = document.KnownTerms
                .Select(t => new LanguageTitleCasingRulesResponse.KnownTermDto
                {
                    Literal = t.Literal,
                    Pattern = t.Pattern,
                    Options = t.Options
                })
                .ToList()
        };
}
