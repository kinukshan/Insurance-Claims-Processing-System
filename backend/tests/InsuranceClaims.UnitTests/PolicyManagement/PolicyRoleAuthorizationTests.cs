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
}
