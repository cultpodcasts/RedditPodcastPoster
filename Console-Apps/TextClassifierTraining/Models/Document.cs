namespace TextClassifierTraining.Models;

public class Document
{
    public string Location { get; set; }
    public string Language { get; set; } = "en";
    public string? DataSet { get; set; } = null;
    public Class Class { get; set; }
}