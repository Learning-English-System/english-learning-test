namespace Test.Application.Interfaces;

public interface IAiDistractorClient
{
    Task<IReadOnlyList<string>> GenerateDistractorsAsync(
        string word,
        string correctMeaning,
        int count = 3,
        CancellationToken cancellationToken = default);
}
