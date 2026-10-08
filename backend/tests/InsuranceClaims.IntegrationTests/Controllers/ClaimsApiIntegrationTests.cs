using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceClaims.IntegrationTests.Controllers;

public class ClaimsApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ClaimsApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IT05_CreateClaim_ValidDraft_Returns201Created()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        await _factory.SeedReferenceDataAsync(async db =>
        {
            var user = new User
            {
                Id = userId,
                Email = $"claimant_{userId}@example.com",
                FirstName = "Alice",
                LastName = "Smith",
                Role = Role.Policyholder,
                IsActive = true
            };

            var policy = new Policy
            {
                Id = policyId,
                PolicyNumber = $"POL-TEST-{Guid.NewGuid().ToString()[..6]}",
                PolicyholderId = userId,
                PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
                CoverageLimit = 500000m,
                Deductible = 5000m,
                Premium = 1500m,
                Status = PolicyStatus.Active,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                ExpiryDate = DateTime.UtcNow.AddMonths(11)
            };

            db.Users.Add(user);
            db.Policies.Add(policy);
            await db.SaveChangesAsync();
        });

        var token = _factory.GenerateTestToken(userId, $"claimant_{userId}@example.com", Role.Policyholder);

        var createDto = new CreateClaimDto(
            PolicyId: policyId,
            ClaimType: ClaimType.Motor,
            IncidentDate: DateTime.UtcNow.AddDays(-2),
            IncidentLocation: "Colombo 07, Main Road",
            Description: "Rear bumper collision with stationary signpost",
            ClaimedAmount: 25000m
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/claims")
        {
            Content = JsonContent.Create(createDto)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 201 Created and persisted
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var claim = await response.Content.ReadFromJsonAsync<ClaimResponseDto>(_jsonOptions);

        Assert.NotNull(claim);
        Assert.StartsWith("CLM-", claim.ClaimNumber);
        Assert.Equal(createDto.ClaimedAmount, claim.ClaimedAmount);
        Assert.Equal("Draft", claim.Status);

        // Verify persistence in EF Core
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbClaim = await db.Claims.FindAsync(claim.Id);
        Assert.NotNull(dbClaim);
        Assert.Equal(claim.ClaimNumber, dbClaim.ClaimNumber);
    }

    [Fact]
    public async Task IT06_GetClaimById_ReturnsMatchingClaimDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        await _factory.SeedReferenceDataAsync(async db =>
        {
            var user = new User
            {
                Id = userId,
                Email = $"getclaim_{userId}@example.com",
                Role = Role.Policyholder,
                IsActive = true
            };

            var claim = new Claim
            {
                Id = claimId,
                ClaimNumber = $"CLM-2026-{Guid.NewGuid().ToString()[..6]}",
                PolicyId = policyId,
                PolicyHolderId = userId,
                ClaimType = ClaimType.Motor,
                IncidentDate = DateTime.UtcNow.AddDays(-5),
                IncidentLocation = "Kandy Road",
                Description = "Side mirror and door damage",
                ClaimedAmount = 45000m,
                Status = ClaimStatus.Submitted
            };

            db.Users.Add(user);
            db.Claims.Add(claim);
            await db.SaveChangesAsync();
        });

        var token = _factory.GenerateTestToken(userId, $"getclaim_{userId}@example.com", Role.Policyholder);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/claims/{claimId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: 200 OK with correct claim
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var claim = await response.Content.ReadFromJsonAsync<ClaimResponseDto>(_jsonOptions);

        Assert.NotNull(claim);
        Assert.Equal(claimId, claim.Id);
        Assert.Equal(45000m, claim.ClaimedAmount);
    }

    [Fact]
    public async Task IT07_CreateClaim_NegativeOrZeroAmount_Returns400BadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        await _factory.SeedReferenceDataAsync(async db =>
        {
            var user = new User
            {
                Id = userId,
                Email = $"invalid_{userId}@example.com",
                Role = Role.Policyholder,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });

        var token = _factory.GenerateTestToken(userId, $"invalid_{userId}@example.com", Role.Policyholder);

        // Invalid negative claimed amount
        var invalidDto = new CreateClaimDto(
            PolicyId: policyId,
            ClaimType: ClaimType.Motor,
            IncidentDate: DateTime.UtcNow.AddDays(-1),
            IncidentLocation: "Main Street",
            Description: "Invalid claim scenario",
            ClaimedAmount: -5000m
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/claims")
        {
            Content = JsonContent.Create(invalidDto)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Validation boundary rejects invalid input with 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IT08_GetMyClaims_ReturnsClaimsForAuthenticatedPolicyholder()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _factory.SeedReferenceDataAsync(async db =>
        {
            db.Users.AddRange(
                new User { Id = userA, Email = "userA@example.com", Role = Role.Policyholder },
                new User { Id = userB, Email = "userB@example.com", Role = Role.Policyholder }
            );

            db.Claims.AddRange(
                new Claim
                {
                    Id = Guid.NewGuid(),
                    ClaimNumber = $"CLM-A-{Guid.NewGuid().ToString()[..4]}",
                    PolicyHolderId = userA,
                    PolicyId = Guid.NewGuid(),
                    ClaimType = ClaimType.Home,
                    Description = "Claim A",
                    IncidentDate = DateTime.UtcNow.AddDays(-1),
                    ClaimedAmount = 10000m,
                    Status = ClaimStatus.Draft
                },
                new Claim
                {
                    Id = Guid.NewGuid(),
                    ClaimNumber = $"CLM-B-{Guid.NewGuid().ToString()[..4]}",
                    PolicyHolderId = userB,
                    PolicyId = Guid.NewGuid(),
                    ClaimType = ClaimType.Health,
                    Description = "Claim B",
                    IncidentDate = DateTime.UtcNow.AddDays(-1),
                    ClaimedAmount = 20000m,
                    Status = ClaimStatus.Draft
                }
            );
            await db.SaveChangesAsync();
        });

        var tokenA = _factory.GenerateTestToken(userA, "userA@example.com", Role.Policyholder);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/claims/my-claims");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var claims = await response.Content.ReadFromJsonAsync<List<ClaimSummaryDto>>(_jsonOptions);

        Assert.NotNull(claims);
        Assert.NotEmpty(claims);
        // User A should only see their own claim, not user B's
        Assert.All(claims, c => Assert.Contains("CLM-A", c.ClaimNumber));
    }
}
