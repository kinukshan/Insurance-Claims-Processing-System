using System.Text;
using System.Text.Json;
using InsuranceClaims.Api.Controllers;
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

public class PayPalWebhookTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PayoutRepository _payoutRepository;
    private readonly PaymentTransactionRepository _transactionRepository;
    private readonly FakePaymentGateway _paymentGateway;
    private readonly PaymentWebhookController _controller;

    public class FakePaymentGateway : IPaymentGateway
    {
        public bool ValidateResult { get; set; } = true;
        public int ValidateCalls { get; private set; }

        public Task<PaymentGatewayResult> CreatePayoutAsync(PaymentGatewayRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentGatewayResult { Success = true, Provider = "PayPalSandbox" });

        public Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentGatewayStatusResult?>(new PaymentGatewayStatusResult { Success = true, Status = "succeeded" });

        public Task<bool> ValidateWebhookAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            ValidateCalls++;
            return Task.FromResult(ValidateResult);
        }
    }

    public PayPalWebhookTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PayPalWebhookTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _payoutRepository = new PayoutRepository(_context);
        _transactionRepository = new PaymentTransactionRepository(_context);
        _paymentGateway = new FakePaymentGateway();

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

    private void SetupHttpContext(string jsonBody, Dictionary<string, string>? customHeaders = null)
    {
        var httpContext = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(jsonBody);
        httpContext.Request.Body = new MemoryStream(bytes);
        httpContext.Request.ContentLength = bytes.Length;
        httpContext.Request.ContentType = "application/json";

        // Required PayPal transmission headers
        httpContext.Request.Headers["paypal-transmission-id"] = "test-trans-123";
        httpContext.Request.Headers["paypal-transmission-time"] = DateTime.UtcNow.ToString("o");
        httpContext.Request.Headers["paypal-transmission-sig"] = "sig-abc";
        httpContext.Request.Headers["paypal-cert-url"] = "https://api.sandbox.paypal.com/cert";
        httpContext.Request.Headers["paypal-auth-algo"] = "SHA256withRSA";

        if (customHeaders != null)
        {
            foreach (var kv in customHeaders)
            {
                httpContext.Request.Headers[kv.Key] = kv.Value;
            }
        }

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private async Task<(Payout payout, PaymentTransaction transaction)> SeedPayoutAndTransactionAsync(
        decimal amount = 2500m,
        string providerBatchId = "PAYPAL-BATCH-1",
        string providerItemId = "PAYPAL-ITEM-1",
        string senderBatchId = "insurance-payout-1",
        string senderItemId = "claim-1-payout-1")
    {
        var policyholderId = Guid.NewGuid();

        var policyType = new PolicyType { Id = Guid.NewGuid(), Name = "Auto Comprehensive" };
        _context.PolicyTypes.Add(policyType);

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-TEST-001",
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
            ClaimNumber = "CLM-TEST-001",
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
            Status = PayoutStatus.Processing,
            ApprovedBy = "Reviewer Underwriter",
            PaymentReference = providerBatchId
        };
        _context.Payouts.Add(payout);

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            ClaimId = claim.Id,
            Provider = "PayPalSandbox",
            ProviderBatchId = providerBatchId,
            ProviderItemId = providerItemId,
            SenderBatchId = senderBatchId,
            SenderItemId = senderItemId,
            IdempotencyKey = $"payout:{payout.Id}:execution",
            Amount = amount,
            Currency = "USD",
            Recipient = "claimant@example.com",
            Status = PaymentTransactionStatus.Processing
        };
        _context.PaymentTransactions.Add(transaction);

        await _context.SaveChangesAsync();
        return (payout, transaction);
    }

    [Fact]
    public async Task HandlePayPalWebhook_ItemSucceeded_FinalizesPayoutToPaid()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync(3000m, "BATCH-1", "ITEM-1");

        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-EVENT-001",
            event_type = "PAYMENT.PAYOUTS-ITEM.SUCCEEDED",
            resource = new
            {
                payout_batch_id = "BATCH-1",
                payout_item_id = "ITEM-1",
                transaction_id = "PAYPAL-TXN-999",
                transaction_status = "SUCCESS",
                payout_item = new
                {
                    recipient_type = "EMAIL",
                    receiver = "claimant@example.com",
                    sender_item_id = transaction.SenderItemId,
                    amount = new
                    {
                        value = "3000.00",
                        currency = "USD"
                    }
                }
            }
        });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify transaction is Succeeded
        var updatedTxn = await _context.PaymentTransactions.FindAsync(transaction.Id);
        Assert.NotNull(updatedTxn);
        Assert.Equal(PaymentTransactionStatus.Succeeded, updatedTxn.Status);
        Assert.Equal("PAYPAL-TXN-999", updatedTxn.ProviderTransactionId);
        Assert.NotNull(updatedTxn.CompletedAt);

        // Verify payout is Paid
        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Paid, updatedPayout.Status);

        // Verify webhook event recorded in ledger
        var hasProcessed = await _transactionRepository.HasWebhookEventBeenProcessedAsync("PayPal", "WH-EVENT-001");
        Assert.True(hasProcessed);
    }

    [Fact]
    public async Task HandlePayPalWebhook_BatchSuccessAlone_DoesNotFinalizePayout()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync(2000m, "BATCH-2", "ITEM-2");

        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-EVENT-002",
            event_type = "PAYMENT.PAYOUTSBATCH.SUCCESS",
            resource = new
            {
                batch_header = new
                {
                    payout_batch_id = "BATCH-2",
                    batch_status = "SUCCESS",
                    sender_batch_header = new
                    {
                        sender_batch_id = transaction.SenderBatchId
                    }
                }
            }
        });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        Assert.IsType<OkObjectResult>(result);

        // CRITICAL: Batch success does NOT mark transaction or payout Succeeded / Paid!
        var updatedTxn = await _context.PaymentTransactions.FindAsync(transaction.Id);
        Assert.NotNull(updatedTxn);
        Assert.Equal(PaymentTransactionStatus.Processing, updatedTxn.Status);

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Processing, updatedPayout.Status);
        Assert.NotEqual(PayoutStatus.Paid, updatedPayout.Status);
    }

    [Fact]
    public async Task HandlePayPalWebhook_ItemFailed_MarksTransactionAndPayoutFailed()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync(1500m, "BATCH-3", "ITEM-3");

        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-EVENT-003",
            event_type = "PAYMENT.PAYOUTS-ITEM.FAILED",
            resource = new
            {
                payout_batch_id = "BATCH-3",
                payout_item_id = "ITEM-3",
                errors = new[]
                {
                    new { name = "RECEIVER_UNREGISTERED", message = "Receiver account does not exist." }
                }
            }
        });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        Assert.IsType<OkObjectResult>(result);

        var updatedTxn = await _context.PaymentTransactions.FindAsync(transaction.Id);
        Assert.NotNull(updatedTxn);
        Assert.Equal(PaymentTransactionStatus.Failed, updatedTxn.Status);
        Assert.Equal("RECEIVER_UNREGISTERED", updatedTxn.FailureCode);

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Failed, updatedPayout.Status);
    }

    [Fact]
    public async Task HandlePayPalWebhook_ItemUnclaimed_SetsUnclaimed_PayoutRemainsNotPaid()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync(1000m, "BATCH-4", "ITEM-4");

        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-EVENT-004",
            event_type = "PAYMENT.PAYOUTS-ITEM.UNCLAIMED",
            resource = new
            {
                payout_batch_id = "BATCH-4",
                payout_item_id = "ITEM-4"
            }
        });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        Assert.IsType<OkObjectResult>(result);

        var updatedTxn = await _context.PaymentTransactions.FindAsync(transaction.Id);
        Assert.NotNull(updatedTxn);
        Assert.Equal(PaymentTransactionStatus.Unclaimed, updatedTxn.Status);

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Processing, updatedPayout.Status); // Still processing (Awaiting Recipient)
        Assert.NotEqual(PayoutStatus.Paid, updatedPayout.Status);
    }

    [Fact]
    public async Task HandlePayPalWebhook_DuplicateEvent_ReturnsAlreadyProcessed_NoOp()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync(1000m, "BATCH-5", "ITEM-5");

        // Seed existing event in ledger
        await _transactionRepository.AddWebhookEventAsync(new PaymentWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = "PayPal",
            ProviderEventId = "WH-DUP-001",
            PaymentTransactionId = transaction.Id,
            EventType = "PAYMENT.PAYOUTS-ITEM.SUCCEEDED",
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            ProcessingStatus = "Processed"
        });

        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-DUP-001",
            event_type = "PAYMENT.PAYOUTS-ITEM.SUCCEEDED",
            resource = new { payout_item_id = "ITEM-5" }
        });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Payout should not have changed from this duplicate event
        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Processing, updatedPayout.Status);
    }

    [Fact]
    public async Task HandlePayPalWebhook_AmountMismatch_RejectsAndDoesNotFinalize()
    {
        var (payout, transaction) = await SeedPayoutAndTransactionAsync(5000m, "BATCH-6", "ITEM-6");

        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-MISMATCH-001",
            event_type = "PAYMENT.PAYOUTS-ITEM.SUCCEEDED",
            resource = new
            {
                payout_item_id = "ITEM-6",
                payout_item = new
                {
                    amount = new
                    {
                        value = "9999.00", // Mismatched amount
                        currency = "USD"
                    }
                }
            }
        });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        // Transaction and payout must not be finalized
        var updatedTxn = await _context.PaymentTransactions.FindAsync(transaction.Id);
        Assert.NotNull(updatedTxn);
        Assert.NotEqual(PaymentTransactionStatus.Succeeded, updatedTxn.Status);

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.NotEqual(PayoutStatus.Paid, updatedPayout.Status);
    }

    [Fact]
    public async Task HandlePayPalWebhook_InvalidSignature_ReturnsUnauthorized()
    {
        _paymentGateway.ValidateResult = false; // Verification fails
        var payload = JsonSerializer.Serialize(new { id = "WH-INV-001", event_type = "PAYMENT.PAYOUTS-ITEM.SUCCEEDED" });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task HandlePayPalWebhook_MissingRequiredHeaders_ReturnsBadRequest()
    {
        var payload = JsonSerializer.Serialize(new { id = "WH-HDR-001", event_type = "PAYMENT.PAYOUTS-ITEM.SUCCEEDED" });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        // Omit all paypal-* transmission headers

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.HandlePayPalWebhook();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task HandlePayPalWebhook_UnknownTransaction_ReturnsNotFound_Safely()
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-UNKNOWN-001",
            event_type = "PAYMENT.PAYOUTS-ITEM.SUCCEEDED",
            resource = new
            {
                payout_item_id = "UNKNOWN-ITEM-999"
            }
        });

        SetupHttpContext(payload);

        var result = await _controller.HandlePayPalWebhook();

        Assert.IsType<NotFoundObjectResult>(result);

        // Unmatched event recorded in ledger
        var hasRecorded = await _transactionRepository.HasWebhookEventBeenProcessedAsync("PayPal", "WH-UNKNOWN-001");
        Assert.True(hasRecorded);
    }
}
