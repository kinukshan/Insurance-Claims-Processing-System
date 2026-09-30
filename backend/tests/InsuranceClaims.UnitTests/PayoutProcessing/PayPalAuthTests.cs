using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

public class PayPalAuthTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public List<HttpRequestMessage> SentRequests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            return Task.FromResult(ResponseFactory(request));
        }
    }

    private static IConfiguration CreateConfig(string mode = "Sandbox", string clientId = "test-client-id", string clientSecret = "test-secret")
    {
        var dict = new Dictionary<string, string?>
        {
            ["PaymentGateway:PayPal:Mode"] = mode,
            ["PaymentGateway:PayPal:BaseUrl"] = "https://api-m.sandbox.paypal.com",
            ["PaymentGateway:PayPal:ClientId"] = clientId,
            ["PaymentGateway:PayPal:ClientSecret"] = clientSecret
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task GetAccessTokenAsync_RequestsToken_WithBasicAuthAndClientCredentials()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = req =>
            {
                Assert.Equal(HttpMethod.Post, req.Method);
                Assert.Equal("/v1/oauth2/token", req.RequestUri?.AbsolutePath);
                Assert.NotNull(req.Headers.Authorization);
                Assert.Equal("Basic", req.Headers.Authorization.Scheme);

                // Verify basic auth credentials
                var authBytes = Convert.FromBase64String(req.Headers.Authorization.Parameter!);
                var authStr = System.Text.Encoding.UTF8.GetString(authBytes);
                Assert.Equal("test-client-id:test-secret", authStr);

                var json = JsonSerializer.Serialize(new
                {
                    access_token = "mock-access-token-12345",
                    token_type = "Bearer",
                    expires_in = 3600
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var authService = new PayPalAuthService(httpClient, CreateConfig(), NullLogger<PayPalAuthService>.Instance);

        var token = await authService.GetAccessTokenAsync();

        Assert.Equal("mock-access-token-12345", token);
        Assert.Single(handler.SentRequests);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CachesToken_DoesNotRefetchBeforeExpiry()
    {
        int requestCount = 0;
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ =>
            {
                requestCount++;
                var json = JsonSerializer.Serialize(new
                {
                    access_token = $"token-call-{requestCount}",
                    token_type = "Bearer",
                    expires_in = 3600
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var authService = new PayPalAuthService(httpClient, CreateConfig(), NullLogger<PayPalAuthService>.Instance);

        var token1 = await authService.GetAccessTokenAsync();
        var token2 = await authService.GetAccessTokenAsync();

        Assert.Equal("token-call-1", token1);
        Assert.Equal("token-call-1", token2);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Refreshes_WhenTokenIsExpiredOrNearExpiry()
    {
        int requestCount = 0;
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ =>
            {
                requestCount++;
                // expires_in = 0 means expired immediately
                var json = JsonSerializer.Serialize(new
                {
                    access_token = $"refreshed-token-{requestCount}",
                    token_type = "Bearer",
                    expires_in = 0
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var authService = new PayPalAuthService(httpClient, CreateConfig(), NullLogger<PayPalAuthService>.Instance);

        var token1 = await authService.GetAccessTokenAsync();
        var token2 = await authService.GetAccessTokenAsync();

        Assert.Equal("refreshed-token-1", token1);
        Assert.Equal("refreshed-token-2", token2);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ThrowsInvalidOperationException_OnFailedAuth()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"invalid_client\"}")
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var authService = new PayPalAuthService(httpClient, CreateConfig(), NullLogger<PayPalAuthService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => authService.GetAccessTokenAsync());
        Assert.Contains("failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RejectsLiveMode_Safely()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var authService = new PayPalAuthService(httpClient, CreateConfig(mode: "Live"), NullLogger<PayPalAuthService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => authService.GetAccessTokenAsync());
        Assert.Contains("Sandbox only", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.SentRequests);
    }

    private class TestLogger<T> : ILogger<T>
    {
        public List<string> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public async Task GetAccessTokenAsync_NeverLogsSecret()
    {
        var testLogger = new TestLogger<PayPalAuthService>();
        var handler = new TestHttpMessageHandler
        {
            ResponseFactory = _ =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "secret-token-value-xyz",
                    token_type = "Bearer",
                    expires_in = 3600
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-m.sandbox.paypal.com") };
        var authService = new PayPalAuthService(httpClient, CreateConfig(clientSecret: "super-secret-key-999"), testLogger);

        await authService.GetAccessTokenAsync();

        // Verify that logger was never called with secret or access token
        Assert.DoesNotContain(testLogger.LoggedMessages, msg => msg.Contains("super-secret-key-999") || msg.Contains("secret-token-value-xyz"));
    }
}
