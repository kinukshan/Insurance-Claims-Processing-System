using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InsuranceClaims.IntegrationTests.AgentWorkflows;

/// <summary>
/// Mandatory Integrated Workflow Test (SE3110 Section 1):
/// Tests the complete business flow spanning:
/// User Authentication -> Policy Lifecycle -> Claim Submission -> Database Verification.
/// </summary>
public class EndToEndWorkflowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EndToEndWorkflowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IT09_CompleteIntegratedWorkflow_PolicyCreation_To_ClaimCreation_And_Persistence()
    {
        // ── Step 1: Precondition / User Setup ──
        var customerId = Guid.NewGuid();
        var customerEmail = $"workflow_user_{customerId.ToString()[..6]}@example.com";

        await _factory.SeedReferenceDataAsync(async db =>
        {
            var customer = new User
            {
                Id = customerId,
                Email = customerEmail,
                FirstName = "Kasun",
                LastName = "Perera",
                Role = Role.Policyholder,
                IsActive = true
            };
            db.Users.Add(customer);
            await db.SaveChangesAsync();
        });

        var customerToken = _factory.GenerateTestToken(customerId, customerEmail, Role.Policyholder);

        // ── Step 2: Policy Creation via API ──
        var createPolicyDto = new CreatePolicyDto
        {
            PolicyholderId = customerId,
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 1500000m,
            Deductible = 10000m,
            StartDate = DateTime.UtcNow.AddDays(1),
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        using var policyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/policies")
        {
            Content = JsonContent.Create(createPolicyDto)
        };
        policyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var policyResponse = await _client.SendAsync(policyRequest);
        Assert.Equal(HttpStatusCode.Created, policyResponse.StatusCode);

        var createdPolicy = await policyResponse.Content.ReadFromJsonAsync<PolicyDto>(_jsonOptions);
        Assert.NotNull(createdPolicy);
        Assert.Equal(customerId, createdPolicy.PolicyholderId);

        // ── Step 3: File Claim Against The Created Policy ──
        var createClaimDto = new CreateClaimDto(
            PolicyId: createdPolicy.Id,
            ClaimType: ClaimType.Motor,
            IncidentDate: DateTime.UtcNow.AddHours(-12),
            IncidentLocation: "Southern Expressway, Exit 4",
            Description: "Front bumper impact with debris on highway",
            ClaimedAmount: 75000m
        );

        using var claimRequest = new HttpRequestMessage(HttpMethod.Post, "/api/claims")
        {
            Content = JsonContent.Create(createClaimDto)
        };
        claimRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var claimResponse = await _client.SendAsync(claimRequest);
        Assert.Equal(HttpStatusCode.Created, claimResponse.StatusCode);

        var createdClaim = await claimResponse.Content.ReadFromJsonAsync<ClaimResponseDto>(_jsonOptions);
        Assert.NotNull(createdClaim);
        Assert.Equal(createdPolicy.Id, createdClaim.PolicyId);
        Assert.Equal(75000m, createdClaim.ClaimedAmount);
        Assert.Equal("Draft", createdClaim.Status);

        // ── Step 4: Verify Claim Retrieval via Policyholder List ──
        using var myClaimsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/claims/my-claims");
        myClaimsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var myClaimsResponse = await _client.SendAsync(myClaimsRequest);
        Assert.Equal(HttpStatusCode.OK, myClaimsResponse.StatusCode);

        var myClaimsList = await myClaimsResponse.Content.ReadFromJsonAsync<List<ClaimSummaryDto>>(_jsonOptions);
        Assert.NotNull(myClaimsList);
        Assert.Contains(myClaimsList, c => c.Id == createdClaim.Id);

        // ── Step 5: Database State & Relationship Verification ──
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var persistedPolicy = await db.Policies
            .Include(p => p.PolicyType)
            .FirstOrDefaultAsync(p => p.Id == createdPolicy.Id);

        var persistedClaim = await db.Claims
            .FirstOrDefaultAsync(c => c.Id == createdClaim.Id);

        Assert.NotNull(persistedPolicy);
        Assert.NotNull(persistedClaim);
        Assert.Equal(persistedPolicy.Id, persistedClaim.PolicyId);
        Assert.Equal(customerId, persistedClaim.PolicyHolderId);
        Assert.True(persistedClaim.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task IT10_MultipleClaimSubmissions_MaintainsDataIntegrity()
    {
        // Tests reliability and database consistency across multiple claim submissions
        var customerId = Guid.NewGuid();
        var customerEmail = $"batch_{customerId.ToString()[..6]}@example.com";
        var policyId = Guid.NewGuid();

        await _factory.SeedReferenceDataAsync(async db =>
        {
            var user = new User { Id = customerId, Email = customerEmail, Role = Role.Policyholder };
            var policy = new Policy
            {
                Id = policyId,
                PolicyNumber = $"POL-BATCH-{Guid.NewGuid().ToString()[..6]}",
                PolicyholderId = customerId,
                PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
                CoverageLimit = 1000000m,
                Deductible = 5000m,
                Premium = 2000m,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                ExpiryDate = DateTime.UtcNow.AddMonths(11)
            };
            db.Users.Add(user);
            db.Policies.Add(policy);
            await db.SaveChangesAsync();
        });

        var token = _factory.GenerateTestToken(customerId, customerEmail, Role.Policyholder);

        // Submit 3 claims in sequence to verify claim counter increments cleanly
        for (int i = 1; i <= 3; i++)
        {
            var claimDto = new CreateClaimDto(
                PolicyId: policyId,
                ClaimType: ClaimType.Motor,
                IncidentDate: DateTime.UtcNow.AddDays(-i),
                IncidentLocation: $"Location #{i}",
                Description: $"Batch claim test #{i}",
                ClaimedAmount: 10000m * i
            );

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/claims")
            {
                Content = JsonContent.Create(claimDto)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // Verify all 3 claims exist with distinct claim numbers in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userClaims = await db.Claims.Where(c => c.PolicyHolderId == customerId).ToListAsync();

        Assert.Equal(3, userClaims.Count);
        var distinctClaimNumbers = userClaims.Select(c => c.ClaimNumber).Distinct().Count();
        Assert.Equal(3, distinctClaimNumbers);
    }
}
