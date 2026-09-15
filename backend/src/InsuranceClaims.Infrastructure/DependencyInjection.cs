using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Infrastructure.Repositories;
using InsuranceClaims.Infrastructure.ExternalServices;

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

        // Repositories
        services.AddScoped<IClaimRepository, ClaimRepository>();

        // Application services
        services.AddScoped<IClaimService, ClaimService>();

        // External services
        services.AddScoped<IDocumentStorageService, LocalFileStorageService>();
        services.AddScoped<IPolicyValidationService, PolicyValidationService>();

        // AI service HTTP client
        services.AddHttpClient<IDocumentVerificationClient, DocumentVerificationClient>(client =>
        {
            var aiServiceUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(aiServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}

