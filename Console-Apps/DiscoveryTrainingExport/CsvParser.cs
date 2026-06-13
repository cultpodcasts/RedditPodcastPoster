using System.Text;

namespace DiscoveryTrainingExport;

internal static class CsvParser
{
    public static IEnumerable<string[]> ReadRows(string path)
    {
        using var reader = new StreamReader(path);
        var header = reader.ReadLine();
        if (header == null)
        {
            yield break;
        }

        while (reader.ReadLine() is { } line)
        {
            yield return ParseLine(line);
        }
    }

    public static Dictionary<string, int> ParseHeader(string path)
    {
        using var reader = new StreamReader(path);
        var header = reader.ReadLine()
                     ?? throw new InvalidOperationException($"Empty CSV: '{path}'.");
        return header
            .Split(',')
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetField(string[] fields, IReadOnlyDictionary<string, int> header, string name)
    {
        return header.TryGetValue(name, out var index) && index < fields.Length
            ? fields[index]
            : string.Empty;
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
