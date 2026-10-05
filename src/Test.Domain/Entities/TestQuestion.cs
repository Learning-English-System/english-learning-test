namespace Test.Domain.Entities;

public class TestQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid VocabularyId { get; set; }
    public string QuestionType { get; set; } = "MultipleChoice";
    public string Prompt { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? SelectedAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public string OptionsJson { get; set; } = "[]";
    public int OrderIndex { get; set; }

    public TestSession Session { get; set; } = null!;
}
