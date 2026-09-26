using System.Security.Claims;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Unit tests for Policyholder vs Staff role authorization and ownership scoping in Policies module.
/// </summary>
public class PolicyRoleAuthorizationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PolicyService _policyService;
    private readonly PoliciesController _controller;

    private readonly Guid _policyholderAId = Guid.NewGuid();
    private readonly Guid _policyholderBId = Guid.NewGuid();
    private readonly Guid _staffUserId = Guid.NewGuid();

    private Policy _policyA = null!;
    private Policy _policyB = null!;

    public PolicyRoleAuthorizationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PolicyRoleAuthTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _policyService = new PolicyService(_context);
        _controller = new PoliciesController(_policyService);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Comprehensive Auto",
            Description = "Full auto coverage",
            BasePremiumRate = 500m
        };
        _context.PolicyTypes.Add(policyType);

        _policyA = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-A-001",
            PolicyholderId = _policyholderAId,
            PolicyTypeId = policyType.Id,
            PolicyType = policyType,
            CoverageLimit = 50000m,
            Premium = 600m,
            Deductible = 500m,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            ExpiryDate = DateTime.UtcNow.AddMonths(11),
            Status = PolicyStatus.Active
        };

        _policyB = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-B-002",
            PolicyholderId = _policyholderBId,
            PolicyTypeId = policyType.Id,
            PolicyType = policyType,
            CoverageLimit = 100000m,
            Premium = 1200m,
            Deductible = 1000m,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            ExpiryDate = DateTime.UtcNow.AddMonths(10),
            Status = PolicyStatus.Active
        };

        _context.Policies.AddRange(_policyA, _policyB);
        _context.SaveChanges();
    }

    private void SetUserContext(Guid userId, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, userId.ToString()),
            new SecurityClaim(ClaimTypes.Role, role),
            new SecurityClaim("nameid", userId.ToString()),
            new SecurityClaim("sub", userId.ToString()),
            new SecurityClaim("role", role)
        }, "TestAuth");

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // 1. Policyholder list returns only own policies
    [Fact]
    public async Task GetAll_Policyholder_ReturnsOnlyOwnPolicies()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var policies = Assert.IsAssignableFrom<IEnumerable<PolicyDto>>(okResult.Value).ToList();

        Assert.Single(policies);
        Assert.Equal(_policyA.Id, policies[0].Id);
        Assert.Equal(_policyholderAId, policies[0].PolicyholderId);
    }

    // 2. Policyholder cannot see another user's policy in list
    [Fact]
    public async Task GetAll_Policyholder_AnotherUsersPoliciesDoNotAppear()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var policies = Assert.IsAssignableFrom<IEnumerable<PolicyDto>>(okResult.Value).ToList();

        Assert.DoesNotContain(policies, p => p.Id == _policyB.Id);
        Assert.DoesNotContain(policies, p => p.PolicyholderId == _policyholderBId);
    }

    // 3. Policyholder can GET own policy by ID
    [Fact]
    public async Task GetById_Policyholder_CanAccessOwnPolicy()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetById(_policyA.Id);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var policy = Assert.IsType<PolicyDto>(okResult.Value);
        Assert.Equal(_policyA.Id, policy.Id);
    }

    // 4. Policyholder cannot GET another user's policy by ID
    [Fact]
    public async Task GetById_Policyholder_CannotAccessAnotherUsersPolicy_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetById(_policyB.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 5. Staff access remains unchanged (can list all and view any policy)
    [Theory]
    [InlineData("ClaimsAdjuster")]
    [InlineData("Underwriter")]
    [InlineData("Admin")]
    public async Task Staff_CanListAllPoliciesAndAccessAnyPolicy(string role)
    {
        SetUserContext(_staffUserId, role);

        var listResult = await _controller.GetAll();
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var policies = Assert.IsAssignableFrom<IEnumerable<PolicyDto>>(okList.Value).ToList();
        Assert.Equal(2, policies.Count);

        var getResultA = await _controller.GetById(_policyA.Id);
        Assert.IsType<OkObjectResult>(getResultA.Result);

        var getResultB = await _controller.GetById(_policyB.Id);
        Assert.IsType<OkObjectResult>(getResultB.Result);
    }

    // 6. Policyholder cannot update another user's policy by ID
    [Fact]
    public async Task Update_Policyholder_CannotUpdateAnotherUsersPolicy_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var dto = new UpdatePolicyDto { CoverageLimit = 99999m };
        var result = await _controller.Update(_policyB.Id, dto);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 7. Policyholder cannot inspect another user's policies via policyholder endpoint
    [Fact]
    public async Task GetByPolicyholder_Policyholder_CannotAccessOtherUser_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetByPolicyholder(_policyholderBId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 8. Policyholder cannot renew another user's policy
    [Fact]
    public async Task Renew_Policyholder_CannotRenewAnotherUsersPolicy_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.Renew(_policyB.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 9. Policyholder cannot calculate premium on another user's policy
    [Fact]
    public async Task CalculatePremium_Policyholder_CannotCalculateForAnotherUser_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.CalculatePremium(_policyB.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 10. Policyholder cannot view coverage for another user's policy
    [Fact]
    public async Task GetCoverage_Policyholder_CannotViewAnotherUsersCoverage_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetCoverage(_policyB.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 11. Policyholder creating a policy has PolicyholderId derived from JWT
    [Fact]
    public async Task Create_Policyholder_DerivesPolicyholderIdFromJwt()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var dto = new CreatePolicyDto
        {
            PolicyholderId = _policyholderBId, // Attempted tampering
            PolicyTypeId = _policyA.PolicyTypeId,
            CoverageLimit = 30000m,
            Deductible = 500m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await _controller.Create(dto);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var createdDto = Assert.IsType<PolicyDto>(createdResult.Value);
        Assert.Equal(_policyholderAId, createdDto.PolicyholderId);
        Assert.NotEqual(_policyholderBId, createdDto.PolicyholderId);
    }

    // 12. Policyholder cannot update their own policy
    [Fact]
    public async Task Update_Policyholder_CannotUpdateOwnPolicy_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var dto = new UpdatePolicyDto { CoverageLimit = 75000m };
        var result = await _controller.Update(_policyA.Id, dto);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 13. ClaimsAdjuster cannot update any policy
    [Fact]
    public async Task Update_ClaimsAdjuster_CannotUpdateAnyPolicy_ReturnsForbid()
    {
        SetUserContext(_staffUserId, "ClaimsAdjuster");

        var dto = new UpdatePolicyDto { CoverageLimit = 75000m };
        var result = await _controller.Update(_policyA.Id, dto);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 14. Underwriter can update permitted fields (CoverageLimit, ExpiryDate, Exclusions)
    [Fact]
    public async Task Update_Underwriter_CanUpdatePermittedFields()
    {
        SetUserContext(_staffUserId, "Underwriter");

        var newExpiry = DateTime.UtcNow.AddMonths(18);
        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 65000m,
            ExpiryDate = newExpiry,
            Exclusions = "Water damage excluded"
        };

        var result = await _controller.Update(_policyA.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PolicyDto>(okResult.Value);
        Assert.Equal(65000m, updated.CoverageLimit);
        Assert.Equal("Water damage excluded", updated.Exclusions);

        var persisted = await _context.Policies.FindAsync(_policyA.Id);
        Assert.NotNull(persisted);
        Assert.Equal(65000m, persisted.CoverageLimit);
        Assert.Equal("Water damage excluded", persisted.Exclusions);
    }

    // 15. Underwriter cannot change status through a tampered request
    [Fact]
    public async Task Update_Underwriter_CannotChangeStatus_ReturnsForbid_AndDoesNotMutateStatus()
    {
        SetUserContext(_staffUserId, "Underwriter");

        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 70000m,
            Status = "Cancelled"
        };

        var result = await _controller.Update(_policyA.Id, dto);

        Assert.IsType<ForbidResult>(result.Result);

        // Verify status in DB remains Active and coverage limit was not partially updated
        var persisted = await _context.Policies.FindAsync(_policyA.Id);
        Assert.NotNull(persisted);
        Assert.Equal(PolicyStatus.Active, persisted.Status);
        Assert.Equal(50000m, persisted.CoverageLimit);
    }

    // 16. Underwriter cannot change deductible through a tampered request (deductible preserved)
    [Fact]
    public async Task Update_Underwriter_CannotChangeDeductible_DeductiblePreserved()
    {
        SetUserContext(_staffUserId, "Underwriter");

        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 60000m,
            Deductible = 99999m // Tampered deductible
        };

        var result = await _controller.Update(_policyA.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PolicyDto>(okResult.Value);
        Assert.Equal(500m, updated.Deductible); // Preserved original

        var persisted = await _context.Policies.FindAsync(_policyA.Id);
        Assert.NotNull(persisted);
        Assert.Equal(500m, persisted.Deductible);
    }

    // 17. Admin can update permitted fields and perform valid status transitions
    [Fact]
    public async Task Update_Admin_CanUpdatePermittedFieldsAndValidStatusTransitions()
    {
        SetUserContext(_staffUserId, "Admin");

        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 80000m,
            Status = "Cancelled",
            Exclusions = "Administrative cancellation"
        };

        var result = await _controller.Update(_policyA.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PolicyDto>(okResult.Value);
        Assert.Equal("Cancelled", updated.Status);
        Assert.Equal(80000m, updated.CoverageLimit);

        var persisted = await _context.Policies.FindAsync(_policyA.Id);
        Assert.NotNull(persisted);
        Assert.Equal(PolicyStatus.Cancelled, persisted.Status);
    }

    // 18. Admin invalid status transition returns BadRequest
    [Fact]
    public async Task Update_Admin_InvalidStatusTransition_ReturnsBadRequest()
    {
        // First cancel policy B
        _policyB.Status = PolicyStatus.Cancelled;
        await _context.SaveChangesAsync();

        SetUserContext(_staffUserId, "Admin");

        // Attempt invalid reactivation of Cancelled policy
        var dto = new UpdatePolicyDto
        {
            Status = "Active"
        };

        var result = await _controller.Update(_policyB.Id, dto);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // 19. Unauthorized requests cause no partial database changes
    [Fact]
    public async Task Update_UnauthorizedRequest_CausesNoPartialDatabaseChanges()
    {
        SetUserContext(_staffUserId, "Underwriter");

        // Underwriter sends a valid coverage limit change together with an unauthorized status change
        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 88888m,
            Status = "Cancelled"
        };

        var result = await _controller.Update(_policyA.Id, dto);

        Assert.IsType<ForbidResult>(result.Result);

        // Verify DB: CoverageLimit must NOT have changed to 88888m
        var persisted = await _context.Policies.FindAsync(_policyA.Id);
        Assert.NotNull(persisted);
        Assert.NotEqual(88888m, persisted.CoverageLimit);
        Assert.Equal(50000m, persisted.CoverageLimit);
        Assert.Equal(PolicyStatus.Active, persisted.Status);
    }

    // 20. Existing policy deductibles are preserved
    [Fact]
    public async Task Update_ExistingPolicyDeductible_IsPreserved()
    {
        SetUserContext(_staffUserId, "Admin");

        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 95000m,
            Deductible = 40000m // Attempted overwrite
        };

        var result = await _controller.Update(_policyA.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PolicyDto>(okResult.Value);
        Assert.Equal(500m, updated.Deductible);

        var persisted = await _context.Policies.FindAsync(_policyA.Id);
        Assert.NotNull(persisted);
        Assert.Equal(500m, persisted.Deductible);
    }

    // 21. Invalid coverage limit (<= 0) is rejected
    [Fact]
    public async Task Update_InvalidCoverageLimit_ReturnsBadRequest()
    {
        SetUserContext(_staffUserId, "Underwriter");

        var dto = new UpdatePolicyDto
        {
            CoverageLimit = -100m
        };

        var result = await _controller.Update(_policyA.Id, dto);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // 22. Invalid expiry date (<= start date) is rejected
    [Fact]
    public async Task Update_InvalidExpiryDate_ReturnsBadRequest()
    {
        SetUserContext(_staffUserId, "Underwriter");

        var dto = new UpdatePolicyDto
        {
            ExpiryDate = _policyA.StartDate.AddDays(-1)
        };

        var result = await _controller.Update(_policyA.Id, dto);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // 23. Editing a cancelled policy cannot silently reactivate it
    [Fact]
    public async Task Update_CancelledPolicy_CannotSilentlyReactivateIt()
    {
        _policyB.Status = PolicyStatus.Cancelled;
        await _context.SaveChangesAsync();

        SetUserContext(_staffUserId, "Underwriter");

        // Underwriter edits expiry date and exclusions on cancelled policy
        var dto = new UpdatePolicyDto
        {
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            Exclusions = "Updated note"
        };

        var result = await _controller.Update(_policyB.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<PolicyDto>(okResult.Value);
        Assert.Equal("Cancelled", updated.Status);

        var persisted = await _context.Policies.FindAsync(_policyB.Id);
        Assert.NotNull(persisted);
        Assert.Equal(PolicyStatus.Cancelled, persisted.Status);
    }

    // 24. Historical payouts are preserved when policy is edited
    [Fact]
    public async Task Update_HistoricalPayouts_PreservedWhenPolicyUpdated()
    {
        // Setup claim and payout for Policy A
        var claim = new InsuranceClaims.Domain.ClaimsManagement.Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = _policyA.Id,
            PolicyHolderId = _policyholderAId,
            ClaimNumber = "CLM-HIST-001",
            ClaimType = InsuranceClaims.Domain.ClaimsManagement.ClaimType.Auto,
            ClaimedAmount = 20000m,
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            Status = InsuranceClaims.Domain.ClaimsManagement.ClaimStatus.Approved
        };
        _context.Claims.Add(claim);

        var payout = new InsuranceClaims.Domain.PayoutProcessing.Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            ApprovedClaimAmount = 20000m,
            CoverageLimit = 50000m,
            Deductible = 500m,
            ProposedPayout = 19500m,
            FinalPayout = 19500m,
            Status = InsuranceClaims.Domain.PayoutProcessing.PayoutStatus.Approved
        };
        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync();

        SetUserContext(_staffUserId, "Underwriter");

        // Edit policy A coverage limit from 50000 to 120000
        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 120000m
        };

        var result = await _controller.Update(_policyA.Id, dto);
        Assert.IsType<OkObjectResult>(result.Result);

        // Verify historical payout remains completely unchanged
        var refreshedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(refreshedPayout);
        Assert.Equal(50000m, refreshedPayout.CoverageLimit);
        Assert.Equal(500m, refreshedPayout.Deductible);
        Assert.Equal(19500m, refreshedPayout.FinalPayout);
    }
}
