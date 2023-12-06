namespace DeleteSearchDocument;

public class SearchIndexConfig
{
    public required Uri Url { get; set; }
    public required string IndexName { get; set; }
    public required string Key { get; set; }
}