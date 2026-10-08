using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceClaims.IntegrationTests.Controllers;

public class PolicyApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PolicyApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IT01_GetPolicyTypes_ReturnsSuccessAndCanonicalList()
    {
        // Arrange
        await _factory.SeedReferenceDataAsync();

        // Act
        var response = await _client.GetAsync("/api/policytypes");

        // Assert: HTTP 200 OK and non-empty list of active policy types
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(array);
        Assert.Contains(array, element => element.GetProperty("name").GetString()!.Contains("Motor")
                                       || element.GetProperty("name").GetString()!.Contains("Auto"));
    }

    [Fact]
    public async Task IT02_GetPolicies_WithPolicyholderToken_ReturnsOnlyUserPolicies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await _factory.SeedReferenceDataAsync(async db =>
        {
            var user = new User
            {
                Id = userId,
                Email = $"user_{userId}@example.com",
                FirstName = "Test",
                LastName = "Policyholder",
                Role = Role.Policyholder,
                IsActive = true
            };

            var otherUser = new User
            {
                Id = otherUserId,
                Email = $"other_{otherUserId}@example.com",
                FirstName = "Other",
                LastName = "User",
                Role = Role.Policyholder,
                IsActive = true
            };

            var motorType = db.PolicyTypes.First(pt => pt.Id == PolicyClaimCompatibility.MotorInsuranceId);

            var userPolicy = new Policy
            {
                Id = Guid.NewGuid(),
                PolicyNumber = $"POL-USR-{Guid.NewGuid().ToString()[..6]}",
                PolicyholderId = userId,
                PolicyTypeId = motorType.Id,
                CoverageLimit = 500000m,
                Deductible = 5000m,
                Premium = 1500m,
                Status = PolicyStatus.Active,
                StartDate = DateTime.UtcNow.AddDays(-10),
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            };

            var otherPolicy = new Policy
            {
                Id = Guid.NewGuid(),
                PolicyNumber = $"POL-OTH-{Guid.NewGuid().ToString()[..6]}",
                PolicyholderId = otherUserId,
                PolicyTypeId = motorType.Id,
                CoverageLimit = 300000m,
                Deductible = 3000m,
                Premium = 1200m,
                Status = PolicyStatus.Active,
                StartDate = DateTime.UtcNow.AddDays(-10),
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            };

            db.Users.AddRange(user, otherUser);
            db.Policies.AddRange(userPolicy, otherPolicy);
            await db.SaveChangesAsync();
        });

        var token = _factory.GenerateTestToken(userId, $"user_{userId}@example.com", Role.Policyholder);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/policies");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 200 OK and strict tenant isolation (only authenticated user's policies)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policies = await response.Content.ReadFromJsonAsync<List<PolicyDto>>(_jsonOptions);

        Assert.NotNull(policies);
        Assert.All(policies, p => Assert.Equal(userId, p.PolicyholderId));
    }

    [Fact]
    public async Task IT03_CreatePolicy_ValidPayload_Returns201CreatedAndPersistsToDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _factory.SeedReferenceDataAsync(async db =>
        {
            var user = new User
            {
                Id = userId,
                Email = $"creator_{userId}@example.com",
                FirstName = "Jane",
                LastName = "Doe",
                Role = Role.Policyholder,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });

        var token = _factory.GenerateTestToken(userId, $"creator_{userId}@example.com", Role.Policyholder);

        var createDto = new CreatePolicyDto
        {
            PolicyholderId = userId,
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 600000m,
            Deductible = 5000m,
            StartDate = DateTime.UtcNow.AddDays(1),
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/policies")
        {
            Content = JsonContent.Create(createDto)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 201 Created and persisted to database
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdPolicy = await response.Content.ReadFromJsonAsync<PolicyDto>(_jsonOptions);
        Assert.NotNull(createdPolicy);
        Assert.Equal(userId, createdPolicy.PolicyholderId);
        Assert.Equal(createDto.CoverageLimit, createdPolicy.CoverageLimit);

        // Verify direct database persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbPolicy = await db.Policies.FindAsync(createdPolicy.Id);
        Assert.NotNull(dbPolicy);
        Assert.Equal(createdPolicy.PolicyNumber, dbPolicy.PolicyNumber);
    }

    [Fact]
    public async Task IT04_GetPolicyById_NonExistentId_Returns404NotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = _factory.GenerateTestToken(userId, "admin@example.com", Role.Admin);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/policies/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
