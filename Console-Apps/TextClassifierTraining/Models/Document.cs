namespace TextClassifierTraining.Models;

public class Document
{
    public required string Location { get; set; }
    public string Language { get; set; } = "en";
    public string? DataSet { get; set; } = null;
    public required Class Class { get; set; }
}