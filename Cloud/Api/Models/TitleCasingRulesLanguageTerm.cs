namespace Api.Models;

public record TitleCasingRulesLanguageTerm(string Language, string Term);

public record TitleCasingRulesLanguageKnownTermAdd(string Language, KnownTermUpdate Term);

public record TitleCasingRulesLanguageKnownTermDelete(string Language, string Literal);
