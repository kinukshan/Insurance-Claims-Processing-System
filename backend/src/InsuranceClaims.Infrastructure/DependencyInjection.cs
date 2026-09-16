using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Application.PolicyManagement.Interfaces;
using InsuranceClaims.Infrastructure.Services;

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

        // TODO: Register repositories, authentication services, external service clients

        return services;
    }
}
