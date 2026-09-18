using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Authentication;
using InsuranceClaims.Application.PolicyManagement.Interfaces;
using InsuranceClaims.Infrastructure.Services;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Infrastructure.Repositories;
using InsuranceClaims.Infrastructure.ExternalServices;
using InsuranceClaims.Application.RiskAssessment.Interfaces;
using InsuranceClaims.Application.RiskAssessment.Services;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using InsuranceClaims.Infrastructure.AgentIntegration;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using InsuranceClaims.Application.Authentication;

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

        // ── Authentication ───────────────────────────────────────────
        services.AddSingleton<PasswordService>();
        services.AddSingleton<JwtService>();
        services.AddScoped<IAuthService, AuthService>();

        // JWT Bearer authentication
        var jwtKey = configuration["Jwt:Key"];
        if (!string.IsNullOrEmpty(jwtKey))
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "InsuranceClaims",
                    ValidAudience = configuration["Jwt:Audience"] ?? "InsuranceClaims.React",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        }

        services.AddAuthorization();

        // Policy Management
        services.AddScoped<IPolicyService, PolicyService>();

        // ── Claims Management ────────────────────────────────────────
        // Repositories
        services.AddScoped<IClaimRepository, ClaimRepository>();

        // Application services
        services.AddScoped<IClaimService, ClaimService>();

        // External services
        services.AddScoped<IDocumentStorageService, LocalFileStorageService>();
        services.AddScoped<IPolicyValidationService, PolicyValidationService>();

        // AI service HTTP client — Document Verification
        services.AddHttpClient<IDocumentVerificationClient, DocumentVerificationClient>(client =>
        {
            var aiServiceUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(aiServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── Risk Assessment ──────────────────────────────────────────
        services.AddScoped<IRiskAssessmentRepository, RiskAssessmentRepository>();
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

        return services;
    }
}
