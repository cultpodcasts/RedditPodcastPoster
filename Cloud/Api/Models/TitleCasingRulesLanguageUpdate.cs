using Api.Models;

namespace Api.Models;

public record TitleCasingRulesLanguageUpdate(
    string Language,
    LanguageTitleCasingRulesUpdateRequest Request);
