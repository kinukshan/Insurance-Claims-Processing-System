using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Application.PolicyManagement.Interfaces;
using InsuranceClaims.Infrastructure.Services;
using InsuranceClaims.Infrastructure.Repositories;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using InsuranceClaims.Infrastructure.AgentIntegration;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;

namespace InsuranceClaims.Infrastructure;

/// <summary>
/// Registers infrastructure services with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")));

        // Policy Management
        services.AddScoped<IPolicyService, PolicyService>();

        // ── Payout Processing ────────────────────────────────────────
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<IPayoutContextProvider, StubPayoutContextProvider>();
        services.AddScoped<IPaymentGateway, SandboxPaymentGateway>();

        // Agent integration: ASP.NET Core → Internal AI Service
        services.AddHttpClient<IPayoutValidationAgentGateway, PayoutValidationAgentGateway>();

        // TODO: Register repositories, authentication services, external service clients

        return services;
    }
}
