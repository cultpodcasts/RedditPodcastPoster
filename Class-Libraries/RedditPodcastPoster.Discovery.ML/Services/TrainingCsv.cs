using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.Discovery.ML.Services;

/// <summary>
/// RFC 4180 style CSV used by the discovery training export and trainer. Quoted fields may contain
/// commas, doubled quotes and line breaks (episode descriptions are multi-line).
/// </summary>
public static class TrainingCsv
{
    public static Dictionary<string, int> ReadHeader(string path)
    {
        using var reader = new StreamReader(path);
        var header = ReadRecords(reader).FirstOrDefault()
                     ?? throw new InvalidOperationException($"Empty CSV: '{path}'.");
        return ToHeaderIndex(header);
    }

    public static Dictionary<string, int> ToHeaderIndex(IEnumerable<string> header) =>
        header
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

    /// <summary>Data records of the file, header excluded.</summary>
    public static IEnumerable<string[]> ReadRows(string path)
    {
        using var reader = new StreamReader(path);
        foreach (var record in ReadRecords(reader).Skip(1))
        {
            yield return record;
        }
    }

    public static IEnumerable<string[]> ReadRecords(TextReader reader)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var recordStarted = false;

        while (reader.Read() is var read && read != -1)
        {
            var c = (char)read;
            recordStarted = true;
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        current.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(current.ToString());
                    current.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(current.ToString());
                    current.Clear();
                    yield return fields.ToArray();
                    fields.Clear();
                    recordStarted = false;
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (recordStarted)
        {
            fields.Add(current.ToString());
            yield return fields.ToArray();
        }
    }

    public static string GetField(string[] fields, IReadOnlyDictionary<string, int> header, string name) =>
        header.TryGetValue(name, out var index) && index < fields.Length
            ? fields[index]
            : string.Empty;

    public static string Escape(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        var text = value switch
        {
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }
}
