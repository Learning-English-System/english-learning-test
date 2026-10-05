using Ai.Contracts.Grpc;
using BuildingBlocks.Grpc.Interceptors;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Kafka;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Security.Constants;
using BuildingBlocks.Security.Extensions;
using BuildingBlocks.Security.ServiceTokens;
using BuildingBlocks.Web.Correlation;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Learning.Contracts.Grpc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Test.Application.Interfaces;
using Test.Infrastructure.Clients;
using Test.Infrastructure.Data;
using Test.Infrastructure.Inbox;
using Test.Infrastructure.Outbox;

namespace Test.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddTestInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("TestDb")
            ?? configuration["TEST_DB_CONNECTION"]
            ?? "Server=localhost,1433;Database=TestDb;User Id=sa;Password=YourStrong@Passw0rd!2026;TrustServerCertificate=True;";

        services.AddDbContext<TestDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(TestDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        services.AddScoped<ITestDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddScoped<IOutboxRepository, SqlTestOutboxRepository>();
        services.AddScoped<IInboxRepository, SqlTestInboxRepository>();

        // Kafka producer & outbox worker
        services.Configure<KafkaProducerConfig>(options =>
        {
            var section = configuration.GetSection("Kafka");
            options.BootstrapServers = section["BootstrapServers"]
                ?? configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? "localhost:9092";
            options.ClientId = section["ClientId"] ?? "test-service-producer";
        });

        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<OutboxPublisherWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxPublisherWorker>());

        // Service token client
        services.AddServiceTokenClient(configuration);

        // Learning gRPC client
        services.AddScoped(sp =>
        {
            var tokenClient = sp.GetRequiredService<IServiceTokenClient>();
            var correlationCtx = sp.GetRequiredService<ICorrelationIdContext>();
            var interceptor = new ClientAuthInterceptor(tokenClient, correlationCtx, SecurityConstants.Scopes.InternalLearningRead);

            string url = configuration["Services:LearningGrpcUrl"] ?? "http://localhost:5003";
            var channel = GrpcChannel.ForAddress(url);
            var invoker = channel.Intercept(interceptor);
            return new LearningInternalService.LearningInternalServiceClient(invoker);
        });

        // AI gRPC client
        services.AddScoped(sp =>
        {
            var tokenClient = sp.GetRequiredService<IServiceTokenClient>();
            var correlationCtx = sp.GetRequiredService<ICorrelationIdContext>();
            var interceptor = new ClientAuthInterceptor(tokenClient, correlationCtx, SecurityConstants.Scopes.InternalAiInvoke);

            string url = configuration["Services:AiGrpcUrl"] ?? "http://localhost:5004";
            var channel = GrpcChannel.ForAddress(url);
            var invoker = channel.Intercept(interceptor);
            return new AiInternalService.AiInternalServiceClient(invoker);
        });

        services.AddScoped<ILearningServiceClient, GrpcLearningServiceClient>();
        services.AddScoped<IAiDistractorClient, GrpcAiDistractorClient>();

        return services;
    }
}
