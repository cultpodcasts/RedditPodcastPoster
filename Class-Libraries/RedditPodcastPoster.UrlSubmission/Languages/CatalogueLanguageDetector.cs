using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.UrlSubmission.Languages;

/// <summary>
/// Script plus function-word detector. No extra ML package: a confident non-English result
/// names an ISO code; English and weak Latin text return a null code.
/// </summary>
public sealed class CatalogueLanguageDetector : ICatalogueLanguageDetector
{
    private const int ExcerptLength = 140;

    private static readonly Dictionary<string, string[]> LatinWords = new(StringComparer.Ordinal)
    {
        ["en"] = ["the", "and", "of", "to", "in", "is", "for", "that", "with", "on", "this", "are", "was"],
        ["es"] = ["el", "la", "los", "las", "de", "del", "que", "en", "una", "por", "con", "para", "como", "este", "esta"],
        ["fr"] = ["le", "la", "les", "des", "une", "est", "dans", "pour", "que", "pas", "sur", "avec", "qui", "nous"],
        ["de"] = ["der", "die", "das", "und", "ein", "eine", "nicht", "mit", "auf", "den", "dem", "ist", "von"],
        ["pt"] = ["os", "as", "uma", "para", "com", "não", "em", "que", "do", "da", "se", "por", "mais"],
        ["it"] = ["il", "lo", "la", "gli", "che", "non", "per", "con", "una", "del", "della", "sono", "questo"],
        ["nl"] = ["het", "een", "van", "niet", "dat", "voor", "met", "zijn", "aan", "ook", "maar", "naar"],
        ["tr"] = ["bir", "ve", "bu", "için", "ile", "çok", "daha", "olan", "değil", "ama", "kadar"],
        ["id"] = ["yang", "dan", "di", "dari", "untuk", "dengan", "ini", "tidak", "akan", "pada", "atau"],
        ["ms"] = ["yang", "dan", "untuk", "dengan", "tidak", "ini", "akan", "dalam", "kepada", "atau"],
        ["fil"] = ["ang", "mga", "ng", "sa", "na", "ay", "hindi", "para", "kung", "nang", "ito"],
        ["vi"] = ["của", "và", "là", "một", "những", "được", "trong", "cho", "không", "với", "này"],
        ["pl"] = ["się", "nie", "jest", "jak", "ale", "jego", "przez", "tylko", "czy", "oraz", "tego"],
        ["sv"] = ["och", "att", "det", "som", "för", "med", "inte", "den", "har", "ett", "till"],
        ["da"] = ["og", "at", "det", "som", "for", "med", "ikke", "den", "har", "til", "på"],
        ["no"] = ["og", "det", "som", "for", "med", "ikke", "den", "har", "til", "på", "en"],
        ["fi"] = ["ja", "on", "ei", "että", "oli", "hän", "mutta", "kun", "niin", "ole", "sen"],
        ["hu"] = ["hogy", "nem", "egy", "van", "meg", "csak", "volt", "mint", "már", "ezt", "vagy"],
        ["cs"] = ["je", "se", "na", "že", "pro", "jsou", "ale", "jako", "aby", "když", "jsem"],
        ["sk"] = ["je", "sa", "na", "že", "pre", "ako", "ale", "aby", "keď", "som", "nie"],
        ["ro"] = ["și", "de", "la", "în", "cu", "pe", "nu", "este", "din", "pentru", "sau"],
        ["is"] = ["og", "að", "ekki", "það", "sem", "er", "til", "fyrir", "en", "við", "þetta"],
        ["cy"] = ["yn", "mae", "ar", "bod", "wedi", "ond", "fel", "gan", "hefyd", "am", "ni"],
        ["af"] = ["die", "van", "en", "is", "nie", "het", "vir", "wat", "met", "sy", "dat"],
        ["sw"] = ["na", "ya", "wa", "kwa", "ni", "katika", "au", "hii", "hiyo", "yake", "lakini"]
    };

    private static readonly string[] ScriptOrder =
    [
        "ko", "ja", "zh", "th", "el", "he", "gu", "ta", "te", "bn", "pa", "si", "hi", "ar", "uk", "bg", "sr", "ru"
    ];

    public CatalogueLanguageDetection Detect(CatalogueLanguageTexts texts)
    {
        var fields = texts.Fields
            .Select(field => (field, Text: field.Text?.Trim() ?? string.Empty))
            .Where(field => field.Text.Length > 0)
            .ToArray();
        if (fields.Length == 0)
        {
            return new CatalogueLanguageDetection(null, null, []);
        }

        var combined = string.Join('\n', fields.Select(field => field.Text));
        var script = DetectScript(combined);
        if (script != null)
        {
            var scriptSignals = fields
                .Where(field => DetectScript(field.Text) == script)
                .Select(field => new CatalogueLanguageSignal(
                    field.field.Key,
                    field.field.Label,
                    Excerpt(field.Text),
                    $"Mostly {EnglishName(script)} script"))
                .ToArray();
            return scriptSignals.Length == 0
                ? new CatalogueLanguageDetection(null, null, [])
                : new CatalogueLanguageDetection(script, EnglishName(script), scriptSignals);
        }

        var scores = new Dictionary<string, (int Hits, HashSet<string> Words)>(StringComparer.Ordinal);
        var fieldHits = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.Ordinal);
        foreach (var (field, text) in fields)
        {
            foreach (var token in Tokens(text))
            {
                foreach (var (code, words) in LatinWords)
                {
                    if (!words.Contains(token))
                    {
                        continue;
                    }

                    if (!scores.TryGetValue(code, out var score))
                    {
                        score = (0, new HashSet<string>(StringComparer.Ordinal));
                    }

                    score.Words.Add(token);
                    scores[code] = (score.Hits + 1, score.Words);
                    if (!fieldHits.TryGetValue(field.Key, out var byCode))
                    {
                        byCode = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                        fieldHits[field.Key] = byCode;
                    }

                    if (!byCode.TryGetValue(code, out var matched))
                    {
                        matched = new HashSet<string>(StringComparer.Ordinal);
                        byCode[code] = matched;
                    }

                    matched.Add(token);
                }
            }
        }

        var ranked = scores
            .Select(pair => (Code: pair.Key, pair.Value.Hits, Distinct: pair.Value.Words.Count))
            .OrderByDescending(row => row.Hits)
            .ThenByDescending(row => row.Distinct)
            .ToArray();
        if (ranked.Length == 0)
        {
            return new CatalogueLanguageDetection(null, null, []);
        }

        var winner = ranked[0];
        var runnerUp = ranked.Length > 1 ? ranked[1].Hits : 0;
        if (winner.Code == "en" || winner.Distinct < 2 || winner.Hits < 3 || winner.Hits < runnerUp + 2)
        {
            return new CatalogueLanguageDetection(null, null, []);
        }

        var signals = new List<CatalogueLanguageSignal>();
        foreach (var (field, text) in fields)
        {
            if (!fieldHits.TryGetValue(field.Key, out var byCode) ||
                !byCode.TryGetValue(winner.Code, out var matched) ||
                matched.Count == 0)
            {
                continue;
            }

            signals.Add(new CatalogueLanguageSignal(
                field.Key,
                field.Label,
                Excerpt(text),
                $"Matched {EnglishName(winner.Code)} words: {string.Join(", ", matched.Order(StringComparer.Ordinal))}"));
        }

        return signals.Count == 0
            ? new CatalogueLanguageDetection(null, null, [])
            : new CatalogueLanguageDetection(winner.Code, EnglishName(winner.Code), signals);
    }

    private static string? DetectScript(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var letters = 0;
        foreach (var ch in text)
        {
            if (!char.IsLetter(ch))
            {
                continue;
            }

            letters++;
            var code = ScriptOf(ch);
            if (code == null)
            {
                continue;
            }

            counts[code] = counts.GetValueOrDefault(code) + 1;
        }

        if (letters < 12)
        {
            return null;
        }

        string? best = null;
        var bestCount = 0;
        foreach (var code in ScriptOrder)
        {
            if (counts.GetValueOrDefault(code) > bestCount)
            {
                best = code;
                bestCount = counts[code];
            }
        }

        return bestCount >= 8 && bestCount * 100 / letters >= 45 ? best : null;
    }

    private static string? ScriptOf(char ch) => ch switch
    {
        >= '\uAC00' and <= '\uD7AF' or >= '\u1100' and <= '\u11FF' => "ko",
        >= '\u3040' and <= '\u30FF' => "ja",
        >= '\u4E00' and <= '\u9FFF' => "zh",
        >= '\u0E00' and <= '\u0E7F' => "th",
        >= '\u0370' and <= '\u03FF' => "el",
        >= '\u0590' and <= '\u05FF' => "he",
        >= '\u0A80' and <= '\u0AFF' => "gu",
        >= '\u0B80' and <= '\u0BFF' => "ta",
        >= '\u0C00' and <= '\u0C7F' => "te",
        >= '\u0980' and <= '\u09FF' => "bn",
        >= '\u0A00' and <= '\u0A7F' => "pa",
        >= '\u0D80' and <= '\u0DFF' => "si",
        >= '\u0900' and <= '\u097F' => ch == '\u0933' ? "mr" : "hi",
        >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F' => ArabicFamily(ch),
        >= '\u0400' and <= '\u04FF' => CyrillicFamily(ch),
        _ => null
    };

    private static string ArabicFamily(char ch) => ch switch
    {
        '\u0679' or '\u0688' or '\u0691' or '\u06BA' or '\u06BE' or '\u06C1' or '\u06D2' => "ur",
        '\u067E' or '\u0686' or '\u0698' or '\u06AF' or '\u06CC' => "fa",
        _ => "ar"
    };

    private static string CyrillicFamily(char ch) => ch switch
    {
        '\u0402' or '\u0408' or '\u0409' or '\u040A' or '\u040B' or '\u040F' => "sr",
        '\u0406' or '\u0407' or '\u0404' or '\u0491' or '\u0456' or '\u0457' or '\u0454' => "uk",
        '\u042A' or '\u044A' => "bg",
        _ => "ru"
    };

    private static IEnumerable<string> Tokens(string text)
    {
        var current = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetter(ch))
            {
                current.Append(ch);
                continue;
            }

            if (current.Length > 1)
            {
                yield return current.ToString();
            }

            current.Clear();
        }

        if (current.Length > 1)
        {
            yield return current.ToString();
        }
    }

    private static string Excerpt(string text) =>
        text.Length <= ExcerptLength ? text : text[..ExcerptLength].TrimEnd() + "…";

    private static string EnglishName(string code)
    {
        var culture = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .FirstOrDefault(item =>
                item.TwoLetterISOLanguageName.Equals(code, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(culture?.EnglishName) ? code : culture.EnglishName;
    }
}
