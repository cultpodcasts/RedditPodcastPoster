namespace Api.Models;

public record TitleCasingRulesLanguageTerm(string Language, string Term);

public record TitleCasingRulesLowerCaseTermAdd(string Language, TitleCasingRulesAddLowerCaseTermRequest Term);

public record TitleCasingRulesIgnoredSubjectAdd(string Language, TitleCasingRulesAddIgnoredSubjectRequest Term);

public record TitleCasingRulesLanguageKnownTermAdd(string Language, KnownTermUpdate Term);

public record TitleCasingRulesLanguageKnownTermDelete(string Language, string Literal);
