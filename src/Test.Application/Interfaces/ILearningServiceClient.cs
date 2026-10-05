namespace Test.Application.Interfaces;

public record VocabularyCandidateDto(
    Guid Id,
    string Word,
    string Translation,
    string PartOfSpeech,
    string CefrLevel,
    string Status,
    int ReviewStage
);

public interface ILearningServiceClient
{
    Task<IReadOnlyList<VocabularyCandidateDto>> GetCandidatesAsync(
        Guid userId,
        int count,
        string? categoryId = null,
        CancellationToken cancellationToken = default);
}
