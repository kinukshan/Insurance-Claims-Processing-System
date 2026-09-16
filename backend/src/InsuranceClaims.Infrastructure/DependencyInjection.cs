using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using InsuranceClaims.Infrastructure.ExternalServices;
using InsuranceClaims.Application.RiskAssessment.Interfaces;
using InsuranceClaims.Application.RiskAssessment.Services;
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

        // Risk Assessment — Repository
        services.AddScoped<IRiskAssessmentRepository, RiskAssessmentRepository>();

        // Risk Assessment — Application Service
        services.AddScoped<IRiskAssessmentService, RiskAssessmentService>();

        // Risk Assessment — AI Client (HttpClient)
        services.AddHttpClient<IAiRiskClient, AiRiskClient>(client =>
        {
            var aiServiceUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(aiServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── Payout Processing ────────────────────────────────────────
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<IPayoutContextProvider, StubPayoutContextProvider>();
        services.AddScoped<IPaymentGateway, SandboxPaymentGateway>();

        // Agent integration: ASP.NET Core → Internal AI Service
        services.AddHttpClient<IPayoutValidationAgentGateway, PayoutValidationAgentGateway>();

        // TODO: Register repositories, authentication services, external service clients for other modules

        return services;
    }
}
