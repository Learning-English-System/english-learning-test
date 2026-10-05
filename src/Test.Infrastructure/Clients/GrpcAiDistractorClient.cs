using Ai.Contracts.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Test.Application.Interfaces;

namespace Test.Infrastructure.Clients;

public class GrpcAiDistractorClient : IAiDistractorClient
{
    private readonly AiInternalService.AiInternalServiceClient _client;
    private readonly ILogger<GrpcAiDistractorClient> _logger;

    public GrpcAiDistractorClient(
        AiInternalService.AiInternalServiceClient client,
        ILogger<GrpcAiDistractorClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GenerateDistractorsAsync(
        string word,
        string correctMeaning,
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GenerateDistractorsAsync(new GenerateDistractorsRequest
            {
                Word = word,
                CorrectMeaning = correctMeaning,
                TargetLanguage = "vi",
                Count = count
            }, cancellationToken: cancellationToken);

            return response.Distractors.ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI distractors for word {Word}, returning fallbacks", word);
            return new[]
            {
                $"{correctMeaning} (thể phủ định)",
                "Ý nghĩa tương đương",
                "Cách dùng khác"
            };
        }
    }
}
