using CommandLine;

namespace PublishR2;

[Verb("languages", isDefault: true, HelpText = "Publish languages list to R2.")]
public class LanguagesRequest;

[Verb("people", HelpText = "Publish People register to R2.")]
public class PeopleRequest;

[Verb("search-suggestions", HelpText = "Publish typeahead match index to R2.")]
public class SearchSuggestionsRequest;

[Verb("homepage", HelpText = "Publish homepage JSON to R2.")]
public class HomepageRequest;

[Verb("subjects", HelpText = "Publish subjects name list to R2.")]
public class SubjectsRequest;

[Verb("lookups", HelpText = "Publish edge lookup JSON (languages, people, search-suggestions, subjects, flairs). Does not publish homepage or feed.")]
public class LookupsRequest;

[Verb("flairs", HelpText = "Publish subject flairs to R2 (from Cosmos).")]
public class FlairsRequest;

[Verb("all", HelpText = "Publish all R2 targets (languages, people, search-suggestions, homepage), then flairs.")]
public class AllRequest;
