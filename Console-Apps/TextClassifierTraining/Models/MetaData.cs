namespace TextClassifierTraining.Models;

public class MetaData
{
    public string ProjectKind { get; set; } = "CustomSingleLabelClassification";
    public required string StorageInputContainerName { get; set; }
    public required string ProjectName { get; set; }
    public bool Multilingual { get; set; } = false;
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
}