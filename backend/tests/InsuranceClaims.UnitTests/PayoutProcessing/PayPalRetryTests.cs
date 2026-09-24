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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainClaim = InsuranceClaims.Domain.ClaimsManagement.Claim;
using ClaimStatus = InsuranceClaims.Domain.ClaimsManagement.ClaimStatus;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

public class PayPalRetryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PayoutRepository _payoutRepository;
    private readonly PaymentTransactionRepository _transactionRepository;
    private readonly FakePayoutContextProvider _contextProvider;
    private readonly FakeValidationAgentGateway _validationGateway;
    private readonly PayoutService _payoutService;
    private readonly ConfigurablePayPalPaymentGateway _paymentGateway;
    private readonly PayoutsController _controller;

    public class ConfigurablePayPalPaymentGateway : IPaymentGateway
    {
        public int CreatePayoutCallCount { get; set; }
        public int GetStatusCallCount { get; set; }
        public PaymentGatewayRequest? LastRequest { get; set; }
        public Func<PaymentGatewayRequest, PaymentGatewayResult>? CreateHandler { get; set; }
        public Func<string, PaymentGatewayStatusResult?>? StatusHandler { get; set; }

        public Task<PaymentGatewayResult> CreatePayoutAsync(PaymentGatewayRequest request, CancellationToken cancellationToken = default)
        {
            CreatePayoutCallCount++;
            LastRequest = request;
            if (CreateHandler != null) return Task.FromResult(CreateHandler(request));
            return Task.FromResult(new PaymentGatewayResult
            {
                Success = true,
                Provider = "PayPalSandbox",
                ProviderBatchId = "PAYPAL-BATCH-1",
                ProviderItemId = "PAYPAL-ITEM-1",
                ProviderStatus = "processing"
            });
        }

        public Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default)
        {
            GetStatusCallCount++;
            if (StatusHandler != null) return Task.FromResult(StatusHandler(providerTransactionId));
            return Task.FromResult<PaymentGatewayStatusResult?>(new PaymentGatewayStatusResult
            {
                Success = true,
                Status = "processing",
                ProviderBatchId = providerTransactionId
            });
        }

        public Task<bool> ValidateWebhookAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
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

    public PayPalRetryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PayPalRetryTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _payoutRepository = new PayoutRepository(_context);
        _transactionRepository = new PaymentTransactionRepository(_context);
        _contextProvider = new FakePayoutContextProvider();
        _validationGateway = new FakeValidationAgentGateway();
        _paymentGateway = new ConfigurablePayPalPaymentGateway();

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

    private void SetControllerUser(Guid userId, string name, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private async Task<(DomainClaim claim, Payout payout)> SeedApprovedPayoutAsync(decimal amount = 3000m)
    {
        var policyholderId = Guid.NewGuid();

        var user = new User
        {
            Id = policyholderId,
            Email = "claimant@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = Role.Policyholder,
            IsActive = true
        };
        _context.Users.Add(user);

        var policyType = new PolicyType { Id = Guid.NewGuid(), Name = "Auto Comprehensive" };
        _context.PolicyTypes.Add(policyType);

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-001",
            PolicyholderId = policyholderId,
            PolicyTypeId = policyType.Id,
            CoverageLimit = 50000m,
            Deductible = 500m,
            Premium = 1000m,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Active
        };
        _context.Policies.Add(policy);

        var claim = new DomainClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-001",
            PolicyId = policy.Id,
            PolicyHolderId = policyholderId,
            ClaimedAmount = amount + 500m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            Status = ClaimStatus.Approved
        };
        _context.Claims.Add(claim);

        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            ApprovedClaimAmount = amount + 500m,
            CoverageLimit = 50000m,
            Deductible = 500m,
            ProposedPayout = amount,
            FinalPayout = amount,
            Status = PayoutStatus.Approved,
            ApprovedBy = "Approver Underwriter"
        };
        _context.Payouts.Add(payout);

        await _context.SaveChangesAsync();
        return (claim, payout);
    }

    [Fact]
    public async Task Succeeded_Transaction_Is_Never_Retried_Or_Duplicated()
    {
        var (claim, payout) = await SeedApprovedPayoutAsync(2000m);
        SetControllerUser(Guid.NewGuid(), "Admin", "Admin");

        // Seed a transaction that already succeeded
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            ClaimId = claim.Id,
            Provider = "PayPalSandbox",
            IdempotencyKey = $"payout:{payout.Id}:execution",
            SenderBatchId = $"insurance-payout-{payout.Id}",
            SenderItemId = $"claim-{claim.Id}-payout-{payout.Id}",
            Amount = 2000m,
            Currency = "USD",
            Recipient = "claimant@example.com",
            Status = PaymentTransactionStatus.Succeeded,
            CompletedAt = DateTime.UtcNow
        };
        await _transactionRepository.AddAsync(transaction);

        var response = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal("Succeeded", dto.Status);
        Assert.Equal(0, _paymentGateway.CreatePayoutCallCount); // Provider was never called
    }

    [Fact]
    public async Task Processing_Transaction_Does_Not_Create_New_PayPal_Payout()
    {
        var (claim, payout) = await SeedApprovedPayoutAsync(3500m);
        SetControllerUser(Guid.NewGuid(), "Admin", "Admin");

        // Seed a transaction that is currently processing
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            ClaimId = claim.Id,
            Provider = "PayPalSandbox",
            IdempotencyKey = $"payout:{payout.Id}:execution",
            ProviderBatchId = "BATCH-IN-FLIGHT",
            SenderBatchId = $"insurance-payout-{payout.Id}",
            SenderItemId = $"claim-{claim.Id}-payout-{payout.Id}",
            Amount = 3500m,
            Currency = "USD",
            Recipient = "claimant@example.com",
            Status = PaymentTransactionStatus.Processing
        };
        await _transactionRepository.AddAsync(transaction);

        var response = await _controller.ExecutePayout(payout.Id);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);

        Assert.Equal("Processing", dto.Status);
        Assert.Equal(0, _paymentGateway.CreatePayoutCallCount); // Provider was NOT called to create a second payout
    }

    [Fact]
    public async Task Ambiguous_Outcome_Checks_PayPal_Status_Before_Retry()
    {
        var (claim, payout) = await SeedApprovedPayoutAsync(1200m);
        SetControllerUser(Guid.NewGuid(), "Admin", "Admin");

        // Simulate a transaction marked Failed due to previous network timeout
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            ClaimId = claim.Id,
            Provider = "PayPalSandbox",
            IdempotencyKey = $"payout:{payout.Id}:execution",
            ProviderBatchId = "BATCH-AMBIGUOUS-500",
            SenderBatchId = $"insurance-payout-{payout.Id}",
            SenderItemId = $"claim-{claim.Id}-payout-{payout.Id}",
            Amount = 1200m,
            Currency = "USD",
            Recipient = "claimant@example.com",
            Status = PaymentTransactionStatus.Failed,
            FailureCode = "GATEWAY_EXCEPTION",
            FailureMessage = "504 Gateway Timeout"
        };
        await _transactionRepository.AddAsync(transaction);

        // PayPal actually succeeded on their end during the timeout
        _paymentGateway.StatusHandler = refId => new PaymentGatewayStatusResult
        {
            Success = true,
            Status = "succeeded",
            ProviderTransactionId = "TXN-RECOVERED-123"
        };

        var response = await _controller.ExecutePayout(payout.Id);

        // Status lookup was performed on provider
        Assert.Equal(1, _paymentGateway.GetStatusCallCount);
        // Did NOT send a new create payout request
        Assert.Equal(0, _paymentGateway.CreatePayoutCallCount);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PaymentExecutionResultDto>(okResult.Value);
        Assert.Equal("Succeeded", dto.Status);

        // Payout updated to Paid
        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.Equal(PayoutStatus.Paid, updatedPayout!.Status);
    }

    [Fact]
    public async Task Stable_SenderBatchId_And_SenderItemId_Are_Reused_Across_Requests()
    {
        var (claim, payout) = await SeedApprovedPayoutAsync(4000m);
        SetControllerUser(Guid.NewGuid(), "Admin", "Admin");

        var expectedSenderBatchId = $"insurance-payout-{payout.Id}";
        var expectedSenderItemId = $"claim-{claim.Id}-payout-{payout.Id}";

        var response = await _controller.ExecutePayout(payout.Id);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.NotNull(_paymentGateway.LastRequest);
        Assert.Equal(expectedSenderBatchId, _paymentGateway.LastRequest.SenderBatchId);
        Assert.Equal(expectedSenderItemId, _paymentGateway.LastRequest.SenderItemId);

        // Verify stored in DB
        var txn = await _transactionRepository.GetByIdempotencyKeyAsync($"payout:{payout.Id}:execution");
        Assert.NotNull(txn);
        Assert.Equal(expectedSenderBatchId, txn.SenderBatchId);
        Assert.Equal(expectedSenderItemId, txn.SenderItemId);
    }
}
