namespace TextClassifierTraining.Models;

public class Labels
{
    public string ProjectFileVersion { get; set; } = "2022-05-01";
    public string StringIndexType { get; set; } = "Utf16CodeUnit";
    public MetaData MetaData { get; set; }= new MetaData();
    public Assets Assets { get; set; } = new Assets();
}