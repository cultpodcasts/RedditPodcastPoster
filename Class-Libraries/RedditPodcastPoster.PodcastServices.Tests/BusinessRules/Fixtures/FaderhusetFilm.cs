namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Fixtures;

/// <summary>
/// Regression fixture for issue #1421. The curator searches the Danish original title;
/// TMDB lists the film in English and IMDb lists it under a different English title,
/// so no listed title shares a word with the query.
/// The exact strings are the rule under test, so they are literals.
/// </summary>
internal static class FaderhusetFilm
{
    public const string Query = "Faderhuset";
    public const string TmdbTitle = "Father's House";
    public const string ImdbTitle = "House of Holy Father";
    public const string ImdbId = "tt46484887";
    public const int TmdbId = 1723637;
    public const int Year = 2026;
    public const string Cast = "Wili Wulff Botoft, David Dencik";

    /// <summary>IMDb suggestion feed for "Faderhuset", trimmed to the rows the rule cares about.</summary>
    public const string ImdbSuggestionJson = """
        {"d":[
          {"i":{"height":11989,"imageUrl":"https://m.media-amazon.com/images/M/faderhuset.jpg","width":8091},
           "id":"tt46484887","l":"House of Holy Father","q":"feature","qid":"movie","rank":18146,
           "s":"Wili Wulff Botoft, David Dencik","y":2026},
          {"id":"tt4669654","l":"Badehuset","q":"short","qid":"short","rank":452157,"s":"","y":1989},
          {"id":"tt4859164","l":"The Loud House","q":"TV series","qid":"tvSeries","rank":3360,"s":"","y":2015},
          {"id":"tt6714432","l":"The Loud House Movie","q":"feature","qid":"movie","rank":40000,"s":"","y":2021},
          {"id":"nm14223621","l":"Alexander Holterhuset","rank":1000000,"s":"Actor"}
        ],"q":"faderhuset","v":1}
        """;
}
