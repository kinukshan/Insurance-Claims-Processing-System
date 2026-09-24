using System.Security.Claims;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using IPaymentGateway = InsuranceClaims.Application.PayoutProcessing.Interfaces.IPaymentGateway;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainClaim = InsuranceClaims.Domain.ClaimsManagement.Claim;
using ClaimStatus = InsuranceClaims.Domain.ClaimsManagement.ClaimStatus;
using SecurityClaim = System.Security.Claims.Claim;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

/// <summary>
/// Unit tests verifying:
/// - Mock is the default/local payment provider
/// - Startup succeeds without PayPal credentials
/// - Configuration precedence (PaymentGateway:Provider vs flat PAYMENT_GATEWAY_PROVIDER)
/// - PayPal services are not required during Mock execution
/// - MockPaymentGateway deterministic outcomes (success, processing, failure)
/// - Admin execute with MockPaymentGateway records PaymentTransaction with Provider="Mock"
/// - Provider-info endpoint returns authoritative provider name
/// </summary>
public class MockPaymentProviderSelectionTests
{
    private static IConfiguration CreateConfig(Dictionary<string, string?>? settings = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Jwt:Key"] = "SuperSecretKeyForTestingAtLeast32CharsLong!",
            ["AiService:BaseUrl"] = "http://localhost:8000"
        };

        if (settings != null)
        {
            foreach (var kv in settings)
            {
                dict[kv.Key] = kv.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Default_Provider_Is_Mock_When_Unset()
    {
        var config = CreateConfig();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var gateway = provider.GetRequiredService<IPaymentGateway>();

        Assert.IsType<MockPaymentGateway>(gateway);
    }

    [Fact]
    public void Mock_Startup_Succeeds_With_No_PayPal_Credentials()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["PaymentGateway:Provider"] = "Mock",
            ["PaymentGateway:PayPal:ClientId"] = "",
            ["PaymentGateway:PayPal:ClientSecret"] = "",
            ["PaymentGateway:PayPal:WebhookId"] = ""
        });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var gateway = provider.GetRequiredService<IPaymentGateway>();

        Assert.NotNull(gateway);
        Assert.IsType<MockPaymentGateway>(gateway);

        // PayPalAuthService should NOT be registered in Mock mode
        var payPalAuth = provider.GetService<IPayPalAuthService>();
        Assert.Null(payPalAuth);
    }

    [Fact]
    public void Appsettings_Provider_Takes_Precedence_Over_Flat_Env_Variable()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["PaymentGateway:Provider"] = "Mock",
            ["PAYMENT_GATEWAY_PROVIDER"] = "PayPalSandbox"
        });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var gateway = provider.GetRequiredService<IPaymentGateway>();

        Assert.IsType<MockPaymentGateway>(gateway);
    }

    [Fact]
    public void PayPalSandbox_Provider_Registers_PayPalSandbox_Gateway_And_AuthService()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["PaymentGateway:Provider"] = "PayPalSandbox",
            ["PaymentGateway:PayPal:Mode"] = "Sandbox",
            ["PaymentGateway:PayPal:ClientId"] = "sb-client-id",
            ["PaymentGateway:PayPal:ClientSecret"] = "sb-client-secret"
        });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var gateway = provider.GetRequiredService<IPaymentGateway>();
        var authService = provider.GetService<IPayPalAuthService>();

        Assert.IsType<PayPalSandboxPaymentGateway>(gateway);
        Assert.NotNull(authService);
        Assert.IsType<PayPalAuthService>(authService);
    }

    [Fact]
    public void Live_PayPal_Mode_Strictly_Prohibited()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["PaymentGateway:Provider"] = "PayPalSandbox",
            ["PaymentGateway:PayPal:Mode"] = "Live"
        });

        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(config));

        Assert.Contains("Live PayPal mode is strictly prohibited", ex.Message);
    }

    [Fact]
    public void Unsupported_Provider_Throws_Clear_Exception()
    {
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["PaymentGateway:Provider"] = "UnsupportedGateway"
        });

        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(config));

        Assert.Contains("Unsupported payment gateway provider 'UnsupportedGateway'", ex.Message);
    }

    [Fact]
    public async Task MockPaymentGateway_Default_Success_Outcome_Returns_Succeeded()
    {
        var gateway = new MockPaymentGateway("success");
        var request = new PaymentGatewayRequest
        {
            PayoutId = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            Amount = 1500m,
            Currency = "USD",
            IdempotencyKey = $"payout:{Guid.NewGuid()}:test",
            Description = "Test payment"
        };

        var result = await gateway.CreatePayoutAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Mock", result.Provider);
        Assert.Equal("succeeded", result.ProviderStatus);
        Assert.StartsWith("MOCK-PAY-", result.ProviderTransactionId);
    }

    [Fact]
    public async Task MockPaymentGateway_Processing_Outcome_Returns_Processing()
    {
        var gateway = new MockPaymentGateway("processing");
        var request = new PaymentGatewayRequest
        {
            PayoutId = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            Amount = 1500m,
            Currency = "USD",
            IdempotencyKey = $"payout:{Guid.NewGuid()}:test",
            Description = "Test payment"
        };

        var result = await gateway.CreatePayoutAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Mock", result.Provider);
        Assert.Equal("processing", result.ProviderStatus);
        Assert.StartsWith("MOCK-PAY-", result.ProviderTransactionId);
    }

    [Fact]
    public async Task MockPaymentGateway_Failure_Outcome_Returns_Failed()
    {
        var gateway = new MockPaymentGateway("failure");
        var request = new PaymentGatewayRequest
        {
            PayoutId = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            Amount = 1500m,
            Currency = "USD",
            IdempotencyKey = $"payout:{Guid.NewGuid()}:test",
            Description = "Test payment"
        };

        var result = await gateway.CreatePayoutAsync(request);

        Assert.False(result.Success);
        Assert.Equal("Mock", result.Provider);
        Assert.Equal("failed", result.ProviderStatus);
        Assert.Equal("MOCK_FAILURE", result.FailureCode);
    }

    [Fact]
    public async Task MockPaymentGateway_Idempotency_Returns_Existing_Result()
    {
        var gateway = new MockPaymentGateway("success");
        var key = $"payout:{Guid.NewGuid()}:idempotent";
        var request = new PaymentGatewayRequest
        {
            PayoutId = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            Amount = 2000m,
            Currency = "USD",
            IdempotencyKey = key,
            Description = "First call"
        };

        var result1 = await gateway.CreatePayoutAsync(request);
        var result2 = await gateway.CreatePayoutAsync(request);

        Assert.Same(result1, result2);
        Assert.Equal(result1.ProviderTransactionId, result2.ProviderTransactionId);
    }

    [Fact]
    public void PayoutsController_GetPaymentProviderInfo_Returns_Mock()
    {
        var gateway = new MockPaymentGateway("success");
        var controller = CreateController(gateway, out _);

        var response = controller.GetPaymentProviderInfo();
        var okResult = Assert.IsType<OkObjectResult>(response.Result);

        // Verify anonymous object properties via reflection
        var value = okResult.Value;
        Assert.NotNull(value);
        var providerProp = value.GetType().GetProperty("provider")?.GetValue(value)?.ToString();
        var isMockProp = value.GetType().GetProperty("isMock")?.GetValue(value);

        Assert.Equal("Mock", providerProp);
        Assert.Equal(true, isMockProp);
    }

    [Fact]
    public async Task Admin_Execute_With_MockPaymentGateway_Completes_Flow_Without_External_Calls()
    {
        var mockGateway = new MockPaymentGateway("success");
        var controller = CreateController(mockGateway, out var context);

        var (claim, payout) = await SeedApprovedPayoutAsync(context, 3200m);
        SetControllerUser(controller, Guid.NewGuid(), "Admin Kinukshan", "Admin");

        var response = await controller.ExecutePayout(payout.Id);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal(payout.Id, dto.PayoutId);
        Assert.Equal("Mock", dto.Provider);
        Assert.Equal("Succeeded", dto.Status);
        Assert.NotNull(dto.ProviderTransactionId);
        Assert.StartsWith("MOCK-PAY-", dto.ProviderTransactionId);

        // Check authoritative payout state
        var updatedPayout = await context.Payouts
            .Include(p => p.PaymentTransactions)
            .FirstOrDefaultAsync(p => p.Id == payout.Id);

        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Paid, updatedPayout.Status);
        Assert.Equal(dto.ProviderTransactionId, updatedPayout.PaymentReference);

        // Check PaymentTransaction persistence
        var tx = Assert.Single(updatedPayout.PaymentTransactions);
        Assert.Equal("Mock", tx.Provider);
        Assert.Equal(PaymentTransactionStatus.Succeeded, tx.Status);
        Assert.Equal(3200m, tx.Amount);
        Assert.Equal("USD", tx.Currency);
        Assert.NotNull(tx.CompletedAt);
    }

    private static PayoutsController CreateController(IPaymentGateway gateway, out ApplicationDbContext context)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"MockGatewayTestDb_{Guid.NewGuid()}")
            .Options;

        context = new ApplicationDbContext(options);
        var payoutRepo = new PayoutRepository(context);
        var txRepo = new PaymentTransactionRepository(context);
        var contextProvider = new FakePayoutContextProvider();
        var validationGateway = new FakeValidationAgentGateway();
        var payoutService = new PayoutService(payoutRepo, contextProvider, validationGateway);

        return new PayoutsController(
            payoutService,
            gateway,
            payoutRepo,
            txRepo,
            NullLogger<PayoutsController>.Instance);
    }

    private static void SetControllerUser(PayoutsController controller, Guid userId, string userName, string role)
    {
        var claims = new List<SecurityClaim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, role)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private static async Task<(DomainClaim claim, Payout payout)> SeedApprovedPayoutAsync(
        ApplicationDbContext context, decimal finalPayout)
    {
        var policyholderId = Guid.NewGuid();
        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Comprehensive Auto",
            Description = "Full motor coverage"
        };
        context.PolicyTypes.Add(policyType);

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = $"POL-{Guid.NewGuid():N}"[..12].ToUpper(),
            PolicyholderId = policyholderId,
            PolicyTypeId = policyType.Id,
            PolicyType = policyType,
            CoverageLimit = 25000m,
            Deductible = 500m,
            Premium = 1200m,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            Status = PolicyStatus.Active
        };
        context.Policies.Add(policy);

        var claim = new DomainClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = $"CLM-{Guid.NewGuid():N}"[..12].ToUpper(),
            PolicyId = policy.Id,
            Policy = policy,
            PolicyHolderId = policyholderId,
            ClaimedAmount = finalPayout + 500m,
            IncidentDate = DateTime.UtcNow.AddDays(-10),
            Description = "Auto accident damage",
            Status = ClaimStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };
        context.Claims.Add(claim);

        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            Claim = claim,
            ApprovedClaimAmount = finalPayout + 500m,
            CoverageLimit = 25000m,
            Deductible = 500m,
            ProposedPayout = finalPayout,
            FinalPayout = finalPayout,
            Status = PayoutStatus.Approved,
            ApprovedBy = "Alice Underwriter",
            ApprovalTimestamp = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Payouts.Add(payout);
        await context.SaveChangesAsync();

        return (claim, payout);
    }

    private class FakePayoutContextProvider : IPayoutContextProvider
    {
        public Task<PayoutContext?> GetPayoutContextAsync(Guid claimId)
        {
            return Task.FromResult<PayoutContext?>(new PayoutContext
            {
                ClaimId = claimId,
                ApprovedClaimAmount = 5000m,
                PolicyId = Guid.NewGuid(),
                PolicyType = "Comprehensive Auto",
                ClaimType = "Auto",
                CoverageLimit = 25000m,
                Deductible = 500m
            });
        }
    }

    private class FakeValidationAgentGateway : IPayoutValidationAgentGateway
    {
        public Task<PayoutValidationResult> ValidatePayoutProposalAsync(PayoutValidationRequest request)
        {
            return Task.FromResult(new PayoutValidationResult
            {
                Valid = true,
                RequiresHumanApproval = true,
                Summary = "Valid claim"
            });
        }
    }
}
