using System.Text.Json;
using Azure;
using Azure.Core.Serialization;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Moq;
using RedditPodcastPoster.Search.Models;
using SearchDocument = RedditPodcastPoster.Search.Models.SearchDocument;

namespace CliSearchTests.Fakes;

/// <summary>
/// Stands in for the live episode search index. It enforces the schema the Azure service enforces:
/// a <c>$select</c> naming a field the index does not have, or one that is not retrievable, is rejected with a 400,
/// and a <c>$filter</c> must name a filterable field. The schema is built from <see cref="EpisodeSearchRecord"/>,
/// the type that creates the index, so the fake cannot drift from it. Every stored document is held in the index's
/// own field names, and is read back through <see cref="SearchDocument"/> exactly as the search client reads it.
/// </summary>
public sealed class FakeEpisodeSearchIndex
{
    private const string ContentKindField = "contentKind";
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IList<SearchField> _fields = new FieldBuilder
    {
        Serializer = new JsonObjectSerializer(CamelCase)
    }.Build(typeof(EpisodeSearchRecord));

    private readonly List<Dictionary<string, object?>> _documents = [];

    public void Add(string contentKind, Guid id, string title, string description, string? seriesName,
        params string[] subjects)
    {
        var document = new Dictionary<string, object?>
        {
            ["id"] = id.ToString(),
            [ContentKindField] = contentKind,
            ["title"] = title,
            ["description"] = description,
            ["seriesName"] = seriesName,
            ["subjects"] = subjects
        };
        var unknown = document.Keys.Where(key => _fields.All(field => field.Name != key)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                $"Fake index document uses fields the index does not have: {string.Join(", ", unknown)}.");
        }

        _documents.Add(document);
    }

    public Task<Response<SearchResults<SearchDocument>>> Search(SearchOptions? options)
    {
        var selected = ResolveSelect(options);
        var matching = _documents.Where(ApplyFilter(options));
        var results = matching
            .Select(document => ToSearchDocument(document, selected))
            .Select(document => SearchModelFactory.SearchResult(document, 1d, null))
            .ToList();
        var value = SearchModelFactory.SearchResults(results, null, null, null, null);
        return Task.FromResult(Response.FromValue(value, Mock.Of<Response>()));
    }

    private IReadOnlyList<string> ResolveSelect(SearchOptions? options)
    {
        var retrievable = _fields.Where(field => field.IsHidden != true).Select(field => field.Name).ToArray();
        if (options == null || options.Select.Count == 0)
        {
            return retrievable;
        }

        foreach (var name in options.Select)
        {
            if (!retrievable.Contains(name))
            {
                throw new RequestFailedException(400,
                    $"Invalid expression: Could not find a property named '{name}' on type 'search.document'." +
                    "\r\nParameter name: $select");
            }
        }

        return options.Select.ToArray();
    }

    private Func<Dictionary<string, object?>, bool> ApplyFilter(SearchOptions? options)
    {
        if (string.IsNullOrWhiteSpace(options?.Filter))
        {
            return _ => true;
        }

        var match = System.Text.RegularExpressions.Regex.Match(options.Filter, $"^{ContentKindField} eq '([^']+)'$");
        var kindField = _fields.Single(field => field.Name == ContentKindField);
        if (!match.Success || kindField.IsFilterable != true)
        {
            throw new RequestFailedException(400, $"Invalid expression: unsupported $filter '{options.Filter}'.");
        }

        var kind = match.Groups[1].Value;
        return document => (string?)document[ContentKindField] == kind;
    }

    private static SearchDocument ToSearchDocument(Dictionary<string, object?> document, IReadOnlyList<string> selected)
    {
        var projection = document.Where(pair => selected.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var json = JsonSerializer.Serialize(projection);
        return JsonSerializer.Deserialize<SearchDocument>(json)!;
    }
}