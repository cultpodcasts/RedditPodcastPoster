namespace TextClassifierTraining;

public interface ISubjectCleanser
{
    Task<List<string>> CleanSubjects(List<string> subjects);
}