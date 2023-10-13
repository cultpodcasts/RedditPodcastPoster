using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TextClassifierTraining;

public class SubjectCleanser : ISubjectCleanser
{
    private static readonly Regex BracketedTerm = new(@"\((?'bracketedterm'.*)\)", RegexOptions.Compiled);

    private static readonly List<string> RemovedSubjects = new()
    {
        "iblp",
        "bill gothard",
        "mel lyman",
        "mich\u00E8le lonsdale smith",
        "sarah lawrence college",
        "cult recovery",
        "dwight york",
        "education",
        "one-on-one cult",
        "lig",
        "paul waugh",
        "christopher nash",
        "chris nash",
        "shaun cooper",
        "warren vaughan",
        "a very british cult",
        "ifb",
        "jms",
        "interview with a cult podcaster",
        "therapy cult",
        "lighthouse international",
        "fundraising",
        "cult survivor charity fundraising",
        "uckg",
        "cults in the workplace",
        "ruach center",
        "osho",
        "iskcon",
        "i am",
        "paul mackenzie",
        "moonies",
        "pbcc",
        "philadelphia organization",
        "waco",
        "unregulated therapy",
        "yellow deli",
        "common ground cafe",
        "icoc",
        "foundation of human understanding",
        "us law",
        "episode",
        "annual podcast review",
        "lgats",
        "cult legislation",
        "the move",
        "wellness",
        "political cult",
        "pcg",
        "independent catholicism",
        "ctl",
        "family cults",
        "narcissistic abuse",
        "cults",
        "criminal law and cult psychology",
        "cult news",
        "corporate cult",
        "scientology and qanon",
        "bbc",
        "richard turner",
        "nicky campbell",
        "mark stevens",
        "street epistemology",
        "dr tony quinn",
        "dr. tony quinn",
        "tony quinn",
        "crime world",
        "sunday world",
        "nicola tallant",
        "nicola", "tallant",
        "ian haworth",
        "cult information centre",
        "catalyst",
        "catalyst counselling",
        "graham baldwin",
        "fecris",
        "spiritual narcissist",
        "bikram choudhury",
        "ireland",
        "lgat",
        "seminar",
        "steve collins",
        "paul mckenna",
        "greg dyke",
        "itv",
        "family survival trust",
        "fst",
        "exclusive brethren",
        "plymouth brethren",
        "latter-day saints",
        "marcus fearon",
        "catrin nye",
        "william irvine",
        "12 tribes",
        "activism",
        "cult podcast summary",
        "charity commission",
        "the guardian",
        "finsbury park",
        "maeve mcclenaghan",
        "nosheen iqbal",
        "gwen shamblin",
        "weigh down workshop",
        "peoples temple",
        "jonestown",
        "church of england",
        "cult documentaries",
        "family international",
        "lori vallow",
        "providence",
        "centers of light",
        "malindi",
        "duggars",
        "william kamm",
        "thomas clyde smith jr",
        "frederick lenz",
        "straightway"
    };

    private readonly ILogger<SubjectCleanser> _logger;

    public SubjectCleanser(ILogger<SubjectCleanser> logger)
    {
        _logger = logger;
    }

    public List<string> CleanSubjects(List<string> subjects)
    {
        var splitSubjects = new List<string>();
        foreach (var subject in subjects.Select(x => x.ToLower().Trim()).Distinct())
        {
            List<string> components = new();

            if (subject == "dwell community church / xenos christian fellowship")
            {
                components.Add("dwell community church, xenos");
            }
            else if (subject == "international churches of christ - icoc")
            {
                components.Add("international churches of christ");
                components.Add("icoc");
            }
            else if (subject == "interview with a cult expert")
            {
                components.Add("cult-expert");
            }
            else if (subject == "father bing" || subject == "fr. bing" || subject == "fr bing" || subject == "fr.bing")
            {
                components.Add("father bing");
            }
            else if (subject == "fundamentalist church of jesus christ of latter-day saints")
            {
                components.Add("FLDS Church");
            }
            else if (subject == "fundamentalist church of jesus christ of latter-day saints(flds)")
            {
                components.Add("FLDS");
            }
            else if (subject == "the crusade church & worldwide church of god")
            {
                components.Add("the crusade church");
                components.Add("worldwide church of god");
            }
            else if (subject == "ramana maharshi & meher baba")
            {
                components.Add("ramana maharshi");
                components.Add("meher baba");
            }
            else if (subject == "bomb party (jewelry mlm)")
            {
                components.Add("bomb party");
            }
            else if (subject == "move (philadelphia organization)")
            {
                components.Add("move philadelphia");
            }
            else if (subject.Contains("/"))
            {
                components.AddRange(subject.Split("/").Select(x => x.Trim()));
            }
            else if (subject.Contains("\\"))
            {
                components.AddRange(subject.Split("\\").Select(x => x.Trim()));
            }
            else
            {
                components.Add(subject);
            }

            foreach (var component in components)
            {
                var match = BracketedTerm.Match(component);
                if (match.Success)
                {
                    var bracketedTerm = match.Groups["bracketedterm"].Value;
                    splitSubjects.Add(bracketedTerm.Trim());
                    var cleansed = BracketedTerm.Replace(component, string.Empty).Trim();
                    splitSubjects.Add(cleansed);
                }
                else
                {
                    splitSubjects.Add(component);
                }
            }
        }

        var cleansedSubjects = new List<string>();
        foreach (var subject in splitSubjects)
        {
            var cleansed = subject.Replace("\u0026", "and");
            cleansed = cleansed.Replace("!", string.Empty);
            cleansed = cleansed.Replace("- ", string.Empty);
            if (!RemovedSubjects.Contains(cleansed))
            {
                cleansedSubjects.Add(cleansed);
            }
        }

        return cleansedSubjects;
    }
}