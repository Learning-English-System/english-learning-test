using Grpc.Core;
using Learning.Contracts.Grpc;
using Microsoft.Extensions.Logging;
using Test.Application.Interfaces;

namespace Test.Infrastructure.Clients;

public class GrpcLearningServiceClient : ILearningServiceClient
{
    private readonly LearningInternalService.LearningInternalServiceClient _client;
    private readonly ILogger<GrpcLearningServiceClient> _logger;

    public GrpcLearningServiceClient(
        LearningInternalService.LearningInternalServiceClient client,
        ILogger<GrpcLearningServiceClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VocabularyCandidateDto>> GetCandidatesAsync(
        Guid userId,
        int count,
        string? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetVocabularyCandidatesRequest
            {
                UserId = userId.ToString(),
                Count = count,
                CategoryId = categoryId ?? string.Empty
            };

            var response = await _client.GetVocabularyCandidatesAsync(request, cancellationToken: cancellationToken);

            return response.Items.Select(item => new VocabularyCandidateDto(
                Id: Guid.TryParse(item.Id, out var id) ? id : Guid.NewGuid(),
                Word: item.Word,
                Translation: item.Translation,
                PartOfSpeech: item.PartOfSpeech,
                CefrLevel: item.CefrLevel,
                Status: item.Status,
                ReviewStage: item.ReviewStage
            )).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to query vocabulary candidates for user {UserId}", userId);
            return Array.Empty<VocabularyCandidateDto>();
        }
    }
}
