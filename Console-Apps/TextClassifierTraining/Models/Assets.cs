namespace TextClassifierTraining.Models;

public class Assets
{
    public string ProjectKind { get; set; } = "CustomSingleLabelClassification";
    public IList<Class> Classes { get; set; } = new List<Class>();
    public IList<Document> Documents { get; set; } = new List<Document>();
}