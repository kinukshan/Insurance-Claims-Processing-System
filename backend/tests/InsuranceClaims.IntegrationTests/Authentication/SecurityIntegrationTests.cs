using System.Net;
using System.Net.Http.Headers;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.IntegrationTests.Infrastructure;

namespace InsuranceClaims.IntegrationTests.Authentication;

/// <summary>
/// Non-Functional Security Tests:
/// Verifies Authentication Guard (HTTP 401 Unauthorized),
/// Role-Based Access Control (RBAC) (HTTP 403 Forbidden vs 200 OK),
/// and Token Integrity / Tampering protection.
/// </summary>
public class SecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SecurityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SEC01_UnauthenticatedRequest_ToAuthMe_Returns401Unauthorized()
    {
        // Act: attempt to access protected endpoint without Authorization header
        var response = await _client.GetAsync("/api/auth/me");

        // Assert: must strictly return 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SEC02_UnauthenticatedRequest_ToPolicies_Returns401Unauthorized()
    {
        // Act: attempt to access protected policies without token
        var response = await _client.GetAsync("/api/policies");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SEC03_UnauthenticatedRequest_ToRiskAssessments_Returns401Unauthorized()
    {
        // Act: attempt to access staff risk assessments without token
        var response = await _client.GetAsync("/api/riskassessments");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SEC04_PolicyholderRole_AccessingStaffRiskAssessments_Returns403Forbidden()
    {
        // Arrange: generate valid token for a Policyholder (customer)
        var token = _factory.GenerateTestToken(
            userId: Guid.NewGuid(),
            email: "policyholder@example.com",
            role: Role.Policyholder
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/riskassessments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: customer role MUST be forbidden from accessing internal risk assessments
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SEC05_PolicyholderRole_AccessingStaffPayouts_Returns403Forbidden()
    {
        // Arrange: generate valid token for a Policyholder
        var token = _factory.GenerateTestToken(
            userId: Guid.NewGuid(),
            email: "policyholder@example.com",
            role: Role.Policyholder
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/payouts/calculate/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SEC06_ClaimsAdjusterRole_AccessingRiskAssessments_Returns200OK()
    {
        // Arrange: generate valid token for a ClaimsAdjuster
        var token = _factory.GenerateTestToken(
            userId: Guid.NewGuid(),
            email: "adjuster@example.com",
            role: Role.ClaimsAdjuster
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/riskassessments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: authorized staff member is permitted access
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SEC07_AdminRole_AccessingRiskAssessments_Returns200OK()
    {
        // Arrange: generate valid token for an Admin
        var token = _factory.GenerateTestToken(
            userId: Guid.NewGuid(),
            email: "admin@example.com",
            role: Role.Admin
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/riskassessments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SEC08_TamperedOrMalformedJwt_Returns401Unauthorized()
    {
        // Arrange: create a fake/corrupted token
        var invalidToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalidpayload.invalidsignature";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/policies");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
