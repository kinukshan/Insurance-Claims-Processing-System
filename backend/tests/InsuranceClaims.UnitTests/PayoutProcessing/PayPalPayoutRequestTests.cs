using System.Net;
using System.Text.Json;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

public class PayPalPayoutRequestTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public List<HttpRequestMessage> SentRequests { get; } = new();
        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                RequestBodies.Add(body);
            }
            return ResponseFactory(request);
        }
    }

    private static IConfiguration CreateConfig()
    {
        var dict = new Dictionary<string, string?>
        {
            ["PaymentGateway:PayPal:Mode"] = "Sandbox",
            ["PaymentGateway:PayPal:BaseUrl"] = "https://api-m.sandbox.paypal.com",
            ["PaymentGateway:PayPal:ClientId"] = "client-id",
            ["PaymentGateway:PayPal:ClientSecret"] = "client-secret",
            ["PaymentGateway:PayPal:WebhookId"] = "webhook-id"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private class StubAuthService : IPayPalAuthService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("mock-bearer-token");
    }

    [Fact]
    public async Task CreatePayoutAsync_BuildsCorrectPayPalPayoutsRequest()
    {
        var payoutId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var senderBatchId = $"insurance-payout-{payoutId}";
        var senderItemId = $"claim-{claimId}-payout-{payoutId}";
        var recipientEmail = "claimant@example.com";

        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = req =>
            {
                Assert.Equal(HttpMethod.Post, req.Method);
                Assert.Equal("/v1/payments/payouts", req.RequestUri?.AbsolutePath);
                Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
                Assert.Equal("mock-bearer-token", req.Headers.Authorization?.Parameter);

                var json = JsonSerializer.Serialize(new
                {
                    batch_header = new
                    {
                        payout_batch_id = "PAYPAL-BATCH-123",
                        batch_status = "PENDING"
                    }
                });

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var gateway = new PayPalSandboxPaymentGateway(httpClient, new StubAuthService(), CreateConfig(), NullLogger<PayPalSandboxPaymentGateway>.Instance);

        var request = new PaymentGatewayRequest
        {
            PayoutId = payoutId,
            ClaimId = claimId,
            Amount = 1750.50m,
            Currency = "USD",
            RecipientEmail = recipientEmail,
            SenderBatchId = senderBatchId,
            SenderItemId = senderItemId,
            BeneficiaryReference = "CLAIM-REF-1",
            Description = "Insurance Settlement Payout"
        };

        var result = await gateway.CreatePayoutAsync(request);

        Assert.True(result.Success);
        Assert.Equal("PayPalSandbox", result.Provider);
        Assert.Equal("PAYPAL-BATCH-123", result.ProviderBatchId);
        Assert.Equal("processing", result.ProviderStatus);

        // Verify request payload
        Assert.Single(handler.RequestBodies);
        using var doc = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = doc.RootElement;

        var senderBatchHeader = root.GetProperty("sender_batch_header");
        Assert.Equal(senderBatchId, senderBatchHeader.GetProperty("sender_batch_id").GetString());

        var items = root.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        var item = items[0];
        Assert.Equal("EMAIL", item.GetProperty("recipient_type").GetString());
        Assert.Equal(recipientEmail, item.GetProperty("receiver").GetString());
        Assert.Equal(senderItemId, item.GetProperty("sender_item_id").GetString());

        var amountObj = item.GetProperty("amount");
        Assert.Equal("1750.50", amountObj.GetProperty("value").GetString());
        Assert.Equal("USD", amountObj.GetProperty("currency").GetString());
    }

    [Fact]
    public void CreatePayoutAsync_RejectsLiveMode_Safely()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };

        var liveConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentGateway:PayPal:Mode"] = "Live",
            ["PaymentGateway:PayPal:BaseUrl"] = "https://api-m.sandbox.paypal.com",
            ["PaymentGateway:PayPal:ClientId"] = "id",
            ["PaymentGateway:PayPal:ClientSecret"] = "secret"
        }).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new PayPalSandboxPaymentGateway(httpClient, new StubAuthService(), liveConfig, NullLogger<PayPalSandboxPaymentGateway>.Instance));
        Assert.Contains("Sandbox", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.SentRequests);
    }
}
