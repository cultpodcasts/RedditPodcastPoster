using Api.Dtos;
using RedditPodcastPoster.Models.Languages;

namespace Api.Dtos.Mapping;

public static class SupportedLanguagesResponseBuilder
{
    public static SupportedLanguagesResponse Build(SupportedLanguagesConfig config, bool isDefault) =>
        new()
        {
            IsDefault = isDefault,
            Languages = config.Languages
                .Select(l => new SupportedLanguagesResponse.SupportedLanguageDto
                {
                    Code = l.Code,
                    Name = l.Name
                })
                .ToList()
        };
}
