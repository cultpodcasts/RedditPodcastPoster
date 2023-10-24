namespace TextClassifierTraining.Models;

public class MetaData
{
    public string ProjectKind { get; set; } = "CustomSingleLabelClassification";
    public string StorageInputContainerName { get; set; }
    public string ProjectName { get; set; }
    public bool Multilingual { get; set; } = false;
    public string Description { get; set; }
    public string Language { get; set; } = "en";
}