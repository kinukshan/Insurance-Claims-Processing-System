using System.Net;
using System.Text.Json;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

public class PayPalStatusMappingTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ResponseFactory(request));
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
            => Task.FromResult("mock-token");
    }

    [Theory]
    [InlineData("PENDING", "processing")]
    [InlineData("PROCESSING", "processing")]
    [InlineData("SUCCESS", "succeeded")]
    [InlineData("FAILED", "failed")]
    [InlineData("DENIED", "failed")]
    [InlineData("UNCLAIMED", "unclaimed")]
    [InlineData("RETURNED", "returned")]
    [InlineData("BLOCKED", "blocked")]
    [InlineData("ONHOLD", "on_hold")]
    [InlineData("HELD", "on_hold")]
    [InlineData("REFUNDED", "refunded")]
    [InlineData("REVERSED", "reversed")]
    [InlineData("CANCELED", "failed")]
    public async Task GetPaymentStatusAsync_MapsPayPalStatuses_ToNormalizedStrings(string rawPayPalStatus, string expectedNormalized)
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = req =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    batch_header = new
                    {
                        payout_batch_id = "BATCH-1",
                        batch_status = rawPayPalStatus
                    },
                    items = new[]
                    {
                        new
                        {
                            payout_item_id = "ITEM-1",
                            transaction_status = rawPayPalStatus
                        }
                    }
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var gateway = new PayPalSandboxPaymentGateway(httpClient, new StubAuthService(), CreateConfig(), NullLogger<PayPalSandboxPaymentGateway>.Instance);

        var result = await gateway.GetPaymentStatusAsync("BATCH-1");

        Assert.NotNull(result);
        Assert.Equal(expectedNormalized, result.Status);
        Assert.Equal(rawPayPalStatus, result.RawStatus);
        Assert.True(result.Success);
    }
}
