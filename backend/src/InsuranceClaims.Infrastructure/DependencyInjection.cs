using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
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
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.Name
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
        services.AddScoped<IPayoutContextProvider, EfPayoutContextProvider>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();

        // Payment gateway: Provider selected via configuration ("Mock" or "PayPalSandbox")
        var paymentProvider = configuration["PaymentGateway:Provider"]
            ?? configuration["PAYMENT_GATEWAY_PROVIDER"]
            ?? "Mock";

        if (string.Equals(paymentProvider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<Application.PayoutProcessing.Interfaces.IPaymentGateway, MockPaymentGateway>();
        }
        else if (string.Equals(paymentProvider, "PayPalSandbox", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(paymentProvider, "PayPal", StringComparison.OrdinalIgnoreCase))
        {
            var paypalMode = configuration["PaymentGateway:PayPal:Mode"]
                ?? configuration["PAYPAL_MODE"]
                ?? "Sandbox";

            if (string.Equals(paypalMode, "Live", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Live PayPal mode is strictly prohibited. Only Sandbox is allowed.");
            }

            var paypalBaseUrl = configuration["PaymentGateway:PayPal:BaseUrl"]
                ?? configuration["PAYPAL_BASE_URL"]
                ?? "https://api-m.sandbox.paypal.com";

            services.AddHttpClient<IPayPalAuthService, PayPalAuthService>(client =>
            {
                client.BaseAddress = new Uri(paypalBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient<Application.PayoutProcessing.Interfaces.IPaymentGateway, PayPalSandboxPaymentGateway>(client =>
            {
                client.BaseAddress = new Uri(paypalBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported payment gateway provider '{paymentProvider}'. Supported values: 'Mock', 'PayPalSandbox'.");
        }

        // Agent integration: ASP.NET Core → Internal AI Service
        services.AddHttpClient<IPayoutValidationAgentGateway, PayoutValidationAgentGateway>();

        return services;
    }
}
