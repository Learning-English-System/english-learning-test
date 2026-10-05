using Microsoft.Extensions.DependencyInjection;
using Test.Application.Interfaces;
using Test.Application.Services;

namespace Test.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddTestApplication(this IServiceCollection services)
    {
        services.AddScoped<ITestService, TestService>();
        return services;
    }
}
