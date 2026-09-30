using System.Security.Claims;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
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

public class PaymentWebhookTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PayoutRepository _payoutRepository;
    private readonly PaymentTransactionRepository _transactionRepository;
    private readonly ConfigurableWebhookPaymentGateway _paymentGateway;
    private readonly PaymentWebhookController _controller;

    public PaymentWebhookTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"WebhookTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _payoutRepository = new PayoutRepository(_context);
        _transactionRepository = new PaymentTransactionRepository(_context);
        _paymentGateway = new ConfigurableWebhookPaymentGateway();

        _controller = new PaymentWebhookController(
            _paymentGateway,
            _transactionRepository,
            _payoutRepository,
            NullLogger<PaymentWebhookController>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private void SetWebhookHeaders(Dictionary<string, string>? headers = null)
    {
        var httpContext = new DefaultHttpContext();
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                httpContext.Request.Headers[kv.Key] = kv.Value;
            }
        }
        else
        {
            // Default valid mock webhook header
            httpContext.Request.Headers["X-Mock-Webhook-Token"] = "development-mock-webhook-token";
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private async Task<(Payout payout, PaymentTransaction transaction)> SeedPayoutAndTransactionAsync(
        decimal amount = 3000m,
        PaymentTransactionStatus initialTxStatus = PaymentTransactionStatus.Created,
        PayoutStatus initialPayoutStatus = PayoutStatus.Processing)
    {
        var policyholderId = Guid.NewGuid();

        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Motor Comprehensive",
            Description = "Full coverage"
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
            StartDate = DateTime.UtcNow.AddMonths(-3),
            ExpiryDate = DateTime.UtcNow.AddMonths(9),
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
            ClaimedAmount = amount + 500m,
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            Description = "Vehicle damage",
            Status = ClaimStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };
        _context.Claims.Add(claim);

        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            Claim = claim,
            ApprovedClaimAmount = amount + 500m,
            CoverageLimit = 25000m,
            Deductible = 500m,
            ProposedPayout = amount,
            FinalPayout = amount,
            Status = initialPayoutStatus,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payouts.Add(payout);

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            ClaimId = claim.Id,
            Provider = "Mock",
            ProviderTransactionId = $"MOCK-TX-{Guid.NewGuid():N}"[..16].ToUpper(),
            IdempotencyKey = $"payout:{payout.Id}:execution",
            Amount = amount,
            Currency = "USD",
            Status = initialTxStatus
        };
        _context.PaymentTransactions.Add(transaction);

        await _context.SaveChangesAsync();
        return (payout, transaction);
    }

    // ── 1. processing webhook updates transaction ─────────────────────

    [Fact]
    public async Task Processing_Webhook_Updates_Transaction_Status()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync();
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-1",
            EventType = "payment.processing",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = transaction.Amount
        };

        var result = await _controller.HandleMockWebhook(evt);
        Assert.IsType<OkObjectResult>(result);

        var updatedTx = await _transactionRepository.GetByIdAsync(transaction.Id);
        Assert.NotNull(updatedTx);
        Assert.Equal(PaymentTransactionStatus.Processing, updatedTx.Status);
        Assert.Equal("evt-1", updatedTx.ProviderEventId);
    }

    // ── 2. succeeded webhook updates transaction and payout ───────────

    [Fact]
    public async Task Succeeded_Webhook_Updates_Transaction_And_Payout()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync();
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-2",
            EventType = "payment.succeeded",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = transaction.Amount
        };

        var result = await _controller.HandleMockWebhook(evt);
        Assert.IsType<OkObjectResult>(result);

        var updatedTx = await _transactionRepository.GetByIdAsync(transaction.Id);
        Assert.NotNull(updatedTx);
        Assert.Equal(PaymentTransactionStatus.Succeeded, updatedTx.Status);
        Assert.NotNull(updatedTx.CompletedAt);

        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Paid, updatedPayout.Status);
        Assert.Equal(transaction.ProviderTransactionId, updatedPayout.PaymentReference);
    }

    // ── 3. failed webhook updates transaction and payout ──────────────

    [Fact]
    public async Task Failed_Webhook_Updates_Transaction_And_Payout()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync();
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-3",
            EventType = "payment.failed",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = transaction.Amount,
            FailureCode = "CARD_DECLINED",
            FailureMessage = "Account balance too low."
        };

        var result = await _controller.HandleMockWebhook(evt);
        Assert.IsType<OkObjectResult>(result);

        var updatedTx = await _transactionRepository.GetByIdAsync(transaction.Id);
        Assert.NotNull(updatedTx);
        Assert.Equal(PaymentTransactionStatus.Failed, updatedTx.Status);
        Assert.Equal("CARD_DECLINED", updatedTx.FailureCode);
        Assert.Equal("Account balance too low.", updatedTx.FailureMessage);

        var updatedPayout = await _payoutRepository.GetByIdAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Failed, updatedPayout.Status);
    }

    // ── 4. duplicate webhook is idempotent ────────────────────────────

    [Fact]
    public async Task Duplicate_Webhook_Is_Idempotent_Replay_Ignored()
    {
        var (_, transaction) = await SeedPayoutAndTransactionAsync();
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-same-id",
            EventType = "payment.processing",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = transaction.Amount
        };

        // First delivery
        var result1 = await _controller.HandleMockWebhook(evt);
        Assert.IsType<OkObjectResult>(result1);

        // Second delivery with same EventId
        var result2 = await _controller.HandleMockWebhook(evt);
        var okResult2 = Assert.IsType<OkObjectResult>(result2);

        // Should return already_processed status without error
        Assert.NotNull(okResult2.Value);
    }

    // ── 5. unknown transaction handled safely ─────────────────────────

    [Fact]
    public async Task Unknown_Transaction_Returns_NotFound()
    {
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-unknown",
            EventType = "payment.succeeded",
            ProviderTransactionId = "NONEXISTENT-TRANSACTION-ID",
            Amount = 1000m
        };

        var result = await _controller.HandleMockWebhook(evt);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── 6. invalid signature/token rejected ───────────────────────────

    [Fact]
    public async Task Invalid_Token_Rejected_With_Unauthorized()
    {
        var (_, transaction) = await SeedPayoutAndTransactionAsync();
        _paymentGateway.RejectWebhooks = true;
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-invalid-token",
            EventType = "payment.succeeded",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = transaction.Amount
        };

        var result = await _controller.HandleMockWebhook(evt);
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── 7. provider amount mismatch rejected ──────────────────────────

    [Fact]
    public async Task Provider_Amount_Mismatch_Rejected()
    {
        var (_, transaction) = await SeedPayoutAndTransactionAsync(amount: 3000m);
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-mismatch",
            EventType = "payment.succeeded",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = 99999m // Incorrect amount
        };

        var result = await _controller.HandleMockWebhook(evt);
        Assert.IsType<BadRequestObjectResult>(result);

        // Status must NOT have changed
        var tx = await _transactionRepository.GetByIdAsync(transaction.Id);
        Assert.NotNull(tx);
        Assert.Equal(PaymentTransactionStatus.Created, tx.Status);
    }

    // ── 8. webhook cannot create arbitrary payout ─────────────────────

    [Fact]
    public async Task Webhook_Cannot_Create_Arbitrary_Payout()
    {
        SetWebhookHeaders();

        var initialPayoutCount = await _context.Payouts.CountAsync();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-create-attempt",
            EventType = "payment.succeeded",
            ProviderTransactionId = "ARBITRARY-PAYOUT-TX",
            Amount = 50000m
        };

        var result = await _controller.HandleMockWebhook(evt);
        Assert.IsType<NotFoundObjectResult>(result);

        var finalPayoutCount = await _context.Payouts.CountAsync();
        Assert.Equal(initialPayoutCount, finalPayoutCount);
    }

    // ── 9. succeeded event cannot execute a different payout ──────────

    [Fact]
    public async Task Succeeded_Event_Does_Not_Affect_Different_Payout()
    {
        var (payout1, tx1) = await SeedPayoutAndTransactionAsync(1000m);
        var (payout2, tx2) = await SeedPayoutAndTransactionAsync(2000m);
        SetWebhookHeaders();

        var evt = new PaymentWebhookEventDto
        {
            EventId = "evt-payout1",
            EventType = "payment.succeeded",
            ProviderTransactionId = tx1.ProviderTransactionId!,
            Amount = 1000m
        };

        await _controller.HandleMockWebhook(evt);

        // Payout 1 updated
        var p1 = await _payoutRepository.GetByIdAsync(payout1.Id);
        Assert.NotNull(p1);
        Assert.Equal(PayoutStatus.Paid, p1.Status);

        // Payout 2 UNCHANGED
        var p2 = await _payoutRepository.GetByIdAsync(payout2.Id);
        Assert.NotNull(p2);
        Assert.Equal(PayoutStatus.Processing, p2.Status);
    }

    // ── 10. repeated success does not duplicate payment ───────────────

    [Fact]
    public async Task Repeated_Success_Does_Not_Duplicate_Payment()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync();
        SetWebhookHeaders();

        var evt1 = new PaymentWebhookEventDto
        {
            EventId = "evt-first-success",
            EventType = "payment.succeeded",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = transaction.Amount
        };

        var result1 = await _controller.HandleMockWebhook(evt1);
        Assert.IsType<OkObjectResult>(result1);

        // Second success event with different EventId
        var evt2 = new PaymentWebhookEventDto
        {
            EventId = "evt-second-success",
            EventType = "payment.succeeded",
            ProviderTransactionId = transaction.ProviderTransactionId!,
            Amount = transaction.Amount
        };

        var result2 = await _controller.HandleMockWebhook(evt2);
        var okResult2 = Assert.IsType<OkObjectResult>(result2);

        // Status remains Succeeded and no duplicate records
        var transactions = await _transactionRepository.GetByPayoutIdAsync(payout.Id);
        Assert.Single(transactions);
        Assert.Equal(PaymentTransactionStatus.Succeeded, transactions[0].Status);
    }

    // ── Test Double: Configurable Webhook Gateway ──────────────────────

    private class ConfigurableWebhookPaymentGateway : IPaymentGateway
    {
        public bool RejectWebhooks { get; set; }

        public Task<PaymentGatewayResult> CreatePayoutAsync(
            PaymentGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PaymentGatewayResult
            {
                Success = true,
                Provider = "Mock",
                ProviderTransactionId = $"MOCK-{Guid.NewGuid():N}"[..16],
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
                Status = "succeeded",
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<bool> ValidateWebhookAsync(
            string payload,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken = default)
        {
            if (RejectWebhooks) return Task.FromResult(false);

            // Simple check matching MockPaymentGateway behavior
            if (headers.TryGetValue("X-Mock-Webhook-Token", out var token) &&
                token == "development-mock-webhook-token")
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(true);
        }
    }
}
