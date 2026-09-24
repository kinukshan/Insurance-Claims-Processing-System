using System.Reflection;
using System.Security.Claims;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainClaim = InsuranceClaims.Domain.ClaimsManagement.Claim;
using ClaimStatus = InsuranceClaims.Domain.ClaimsManagement.ClaimStatus;
using SecurityClaim = System.Security.Claims.Claim;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

public class PaymentExecutionTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PayoutRepository _payoutRepository;
    private readonly PaymentTransactionRepository _transactionRepository;
    private readonly FakePayoutContextProvider _contextProvider;
    private readonly FakeValidationAgentGateway _validationGateway;
    private readonly PayoutService _payoutService;
    private readonly ConfigurableMockPaymentGateway _paymentGateway;
    private readonly PayoutsController _controller;

    public PaymentExecutionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentExecTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _payoutRepository = new PayoutRepository(_context);
        _transactionRepository = new PaymentTransactionRepository(_context);
        _contextProvider = new FakePayoutContextProvider();
        _validationGateway = new FakeValidationAgentGateway();
        _paymentGateway = new ConfigurableMockPaymentGateway();

        _payoutService = new PayoutService(_payoutRepository, _contextProvider, _validationGateway);
        _controller = new PayoutsController(
            _payoutService,
            _paymentGateway,
            _payoutRepository,
            _transactionRepository,
            NullLogger<PayoutsController>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private void SetControllerUser(Guid userId, string userName, string role)
    {
        var claims = new List<SecurityClaim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, role)
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private async Task<(DomainClaim claim, Payout payout)> SeedApprovedPayoutAsync(
        decimal finalPayout = 4500m,
        PayoutStatus status = PayoutStatus.Approved)
    {
        var policyholderId = Guid.NewGuid();

        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Comprehensive Auto",
            Description = "Full motor coverage"
        };
        _context.PolicyTypes.Add(policyType);

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
        _context.Policies.Add(policy);

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
        _context.Claims.Add(claim);

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
            Status = status,
            ApprovedBy = "Alice Underwriter",
            ApprovalTimestamp = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        payout.Approvals.Add(new PayoutApproval
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            ReviewerId = Guid.NewGuid(),
            ReviewerName = "Alice Underwriter",
            Decision = ApprovalDecisionType.Approved,
            DecisionTimestamp = DateTime.UtcNow.AddHours(-1),
            Comments = "Approved per policy terms"
        });

        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync();

        return (claim, payout);
    }

    // ── 1. Admin can execute approved payout ──────────────────────────

    [Fact]
    public async Task Admin_Can_Execute_Approved_Payout()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        var response = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal(payout.Id, dto.PayoutId);
        Assert.NotNull(dto.ProviderTransactionId);
        Assert.Equal("Succeeded", dto.Status);

        // Verify Payout transitioned to Paid
        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Paid, updatedPayout.Status);
        Assert.Equal(dto.ProviderTransactionId, updatedPayout.PaymentReference);
    }

    // ── 2, 3, 4. Role Authorization: Policyholder, Adjuster, Underwriter cannot execute ──

    [Fact]
    public void ExecutePayout_Action_Is_Restricted_To_Admin_Only()
    {
        var method = typeof(PayoutsController).GetMethod(nameof(PayoutsController.ExecutePayout));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',').Select(r => r.Trim()).ToList();
        Assert.Contains("Admin", roles);
        Assert.DoesNotContain("Policyholder", roles);
        Assert.DoesNotContain("ClaimsAdjuster", roles);
        Assert.DoesNotContain("Underwriter", roles);
    }

    // ── 5. Unapproved payout cannot execute ───────────────────────────

    [Theory]
    [InlineData(PayoutStatus.Draft)]
    [InlineData(PayoutStatus.PendingApproval)]
    [InlineData(PayoutStatus.Rejected)]
    public async Task Unapproved_Payout_Cannot_Execute(PayoutStatus invalidStatus)
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m, invalidStatus);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        var response = await _controller.ExecutePayout(payout.Id);

        // Controller catches transition conflict and returns 409 Conflict
        Assert.IsType<ConflictObjectResult>(response.Result);

        // Verify gateway was NOT called
        Assert.Equal(0, _paymentGateway.CallCount);
    }

    // ── 6. Missing payout -> 404 ─────────────────────────────────────

    [Fact]
    public async Task Missing_Payout_Returns_NotFound()
    {
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        var response = await _controller.ExecutePayout(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(response.Result);
        Assert.Equal(0, _paymentGateway.CallCount);
    }

    // ── 7. Duplicate execution does not create second provider call ──

    [Fact]
    public async Task Duplicate_Execution_Does_Not_Create_Second_Provider_Call()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        // First execution
        var response1 = await _controller.ExecutePayout(payout.Id);
        Assert.IsType<OkObjectResult>(response1.Result);
        Assert.Equal(1, _paymentGateway.CallCount);

        // Second execution with same payout ID
        var response2 = await _controller.ExecutePayout(payout.Id);
        var okResult2 = Assert.IsType<OkObjectResult>(response2.Result);
        var dto2 = Assert.IsType<PaymentExecutionResultDto>(okResult2.Value);

        // Still only 1 call to provider
        Assert.Equal(1, _paymentGateway.CallCount);
        Assert.Contains("already exists", dto2.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 8. Idempotency key persisted ──────────────────────────────────

    [Fact]
    public async Task IdempotencyKey_Is_Persisted_On_PaymentTransaction()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        await _controller.ExecutePayout(payout.Id);

        var transactions = await _transactionRepository.GetByPayoutIdAsync(payout.Id);
        Assert.Single(transactions);
        Assert.Equal($"payout:{payout.Id}:execution", transactions[0].IdempotencyKey);
    }

    // ── 9. Successful provider result stored ──────────────────────────

    [Fact]
    public async Task Successful_Provider_Result_Stored()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        await _controller.ExecutePayout(payout.Id);

        var transactions = await _transactionRepository.GetByPayoutIdAsync(payout.Id);
        var tx = Assert.Single(transactions);

        Assert.Equal(PaymentTransactionStatus.Succeeded, tx.Status);
        Assert.NotNull(tx.ProviderTransactionId);
        Assert.Equal("Mock", tx.Provider);
        Assert.Equal(4500m, tx.Amount);
        Assert.Equal("USD", tx.Currency);
        Assert.NotNull(tx.CompletedAt);
    }

    // ── 10. Failed provider result stored safely ─────────────────────

    [Fact]
    public async Task Failed_Provider_Result_Stored_Safely()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        _paymentGateway.SimulateFailure = true;
        _paymentGateway.SimulatedFailureCode = "INSUFFICIENT_FUNDS";
        _paymentGateway.SimulatedFailureMessage = "Settlement account has insufficient funds.";

        var response = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);
        Assert.Equal("Failed", dto.Status);

        var transactions = await _transactionRepository.GetByPayoutIdAsync(payout.Id);
        var tx = Assert.Single(transactions);

        Assert.Equal(PaymentTransactionStatus.Failed, tx.Status);
        Assert.Equal("INSUFFICIENT_FUNDS", tx.FailureCode);
        Assert.Equal("Settlement account has insufficient funds.", tx.FailureMessage);
    }

    // ── 11. Provider failure does not mark payout paid ────────────────

    [Fact]
    public async Task Provider_Failure_Does_Not_Mark_Payout_Paid()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        _paymentGateway.SimulateFailure = true;
        await _controller.ExecutePayout(payout.Id);

        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Failed, updatedPayout.Status);
        Assert.NotEqual(PayoutStatus.Paid, updatedPayout.Status);
    }

    // ── 12. Retry reuses idempotency key ──────────────────────────────

    [Fact]
    public async Task Retry_Reuses_IdempotencyKey()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        // First attempt (failed)
        _paymentGateway.SimulateFailure = true;
        await _controller.ExecutePayout(payout.Id);

        // Attempt second execution
        _paymentGateway.SimulateFailure = false;
        var response2 = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response2.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        // Returns existing transaction via idempotency key
        var tx = await _transactionRepository.GetByIdempotencyKeyAsync($"payout:{payout.Id}:execution");
        Assert.NotNull(tx);
        Assert.Equal(tx.Id, dto.TransactionId);
    }

    // ── 13. Concurrent execute requests do not produce duplicate payment ──

    [Fact]
    public async Task Concurrent_Execute_Returns_Existing_Transaction()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        // Pre-create the transaction to simulate a concurrent request having just saved it
        var idempotencyKey = $"payout:{payout.Id}:execution";
        var existingTx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            ClaimId = payout.ClaimId,
            Provider = "Mock",
            ProviderTransactionId = "CONCURRENT-TX-001",
            IdempotencyKey = idempotencyKey,
            Amount = payout.FinalPayout,
            Currency = "USD",
            Status = PaymentTransactionStatus.Processing
        };
        await _transactionRepository.AddAsync(existingTx);

        // Call Execute
        var response = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal(existingTx.Id, dto.TransactionId);
        Assert.Equal("CONCURRENT-TX-001", dto.ProviderTransactionId);
        Assert.Equal(0, _paymentGateway.CallCount); // Gateway was not called
    }

    // ── 14. Existing approval history remains intact ──────────────────

    [Fact]
    public async Task Existing_Approval_History_Remains_Intact_After_Execution()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        await _controller.ExecutePayout(payout.Id);

        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Single(updatedPayout.Approvals);
        Assert.Equal(ApprovalDecisionType.Approved, updatedPayout.Approvals.First().Decision);
        Assert.Equal("Alice Underwriter", updatedPayout.Approvals.First().ReviewerName);
    }

    // ── 15. Reviewer identity remains intact ──────────────────────────

    [Fact]
    public async Task Reviewer_Identity_Remains_Intact_After_Execution()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        await _controller.ExecutePayout(payout.Id);

        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal("Alice Underwriter", updatedPayout.ApprovedBy);
        Assert.Equal("Approved per policy terms", updatedPayout.Approvals.First().Comments);
    }

    // ── 16. Payment transactions query endpoint ──────────────────────

    [Fact]
    public async Task Staff_Can_Retrieve_Payment_Transactions()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(4500m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        await _controller.ExecutePayout(payout.Id);

        var response = await _controller.GetPaymentTransactions(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dtos = Assert.IsType<List<PaymentTransactionDto>>(okResult.Value);

        Assert.Single(dtos);
        Assert.Equal(payout.Id, dtos[0].PayoutId);
        Assert.Equal("Mock", dtos[0].Provider);
    }

    // ── 17. Regression: Admin can execute payout using Claim ID ──────────

    [Fact]
    public async Task Admin_Can_Execute_Payout_Using_ClaimId()
    {
        var (claim, payout) = await SeedApprovedPayoutAsync(3200m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        // Pass claim.Id instead of payout.Id
        var response = await _controller.ExecutePayout(claim.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal(payout.Id, dto.PayoutId);
        Assert.Equal("Succeeded", dto.Status);
        Assert.Equal("Mock", dto.Provider);

        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Paid, updatedPayout.Status);
    }

    // ── 18. Regression: Execution succeeds when policyholder email lookup returns null ──

    [Fact]
    public async Task Execution_Succeeds_When_Policyholder_Email_Is_Null_Or_Missing()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(1800m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        // Execute without creating a User record for policyholder
        var response = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal(payout.Id, dto.PayoutId);
        Assert.Equal("Succeeded", dto.Status);

        var transactions = await _transactionRepository.GetByPayoutIdAsync(payout.Id);
        var tx = Assert.Single(transactions);
        Assert.NotNull(tx.Recipient);
        Assert.Contains("@insurance.internal", tx.Recipient);
    }

    // ── 19. Regression: Mock payment execution has zero external dependencies ──

    [Fact]
    public async Task Mock_Payment_Execution_Requires_No_External_Credentials_And_Marks_Paid()
    {
        var (_, payout) = await SeedApprovedPayoutAsync(5000m);
        SetControllerUser(Guid.NewGuid(), "Admin User", "Admin");

        var response = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal("Succeeded", dto.Status);
        Assert.Equal("Mock", dto.Provider);

        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Paid, updatedPayout.Status);
        Assert.NotNull(updatedPayout.PaymentReference);
        Assert.StartsWith("MOCK-PAY-", updatedPayout.PaymentReference);
    }

    // ── Test Double: Configurable Mock Payment Gateway ────────────────

    private class ConfigurableMockPaymentGateway : IPaymentGateway
    {
        public int CallCount { get; private set; }
        public bool SimulateFailure { get; set; }
        public string? SimulatedFailureCode { get; set; }
        public string? SimulatedFailureMessage { get; set; }
        public string SimulatedStatus { get; set; } = "succeeded";

        public Task<PaymentGatewayResult> CreatePayoutAsync(
            PaymentGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (SimulateFailure)
            {
                return Task.FromResult(new PaymentGatewayResult
                {
                    Success = false,
                    Provider = "Mock",
                    ProviderTransactionId = $"MOCK-FAIL-{Guid.NewGuid():N}"[..16].ToUpper(),
                    ProviderStatus = "failed",
                    FailureCode = SimulatedFailureCode ?? "SIMULATED_FAILURE",
                    Message = SimulatedFailureMessage ?? "Mock payment simulation failed.",
                    CreatedAt = DateTime.UtcNow
                });
            }

            return Task.FromResult(new PaymentGatewayResult
            {
                Success = true,
                Provider = "Mock",
                ProviderTransactionId = $"MOCK-PAY-{Guid.NewGuid():N}"[..16].ToUpper(),
                ProviderStatus = SimulatedStatus,
                Message = "Payment accepted by mock provider.",
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(
            string providerTransactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PaymentGatewayStatusResult?>(new PaymentGatewayStatusResult
            {
                ProviderTransactionId = providerTransactionId,
                Status = SimulatedStatus,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<bool> ValidateWebhookAsync(
            string payload,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
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
