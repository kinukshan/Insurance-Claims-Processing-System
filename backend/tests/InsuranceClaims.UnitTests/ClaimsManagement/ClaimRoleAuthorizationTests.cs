using System.Security.Claims;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.ExternalServices;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainClaim = InsuranceClaims.Domain.ClaimsManagement.Claim;
using SecurityClaim = System.Security.Claims.Claim;

namespace InsuranceClaims.UnitTests.ClaimsManagement;

/// <summary>
/// Unit tests for Policyholder vs Staff role authorization and ownership scoping in Claims module.
/// </summary>
public class ClaimRoleAuthorizationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ClaimRepository _claimRepository;
    private readonly PolicyValidationService _policyValidation;
    private readonly FakeDocumentStorageService _storageService;
    private readonly FakeDocumentVerificationClient _verificationClient;
    private readonly ClaimService _claimService;
    private readonly ClaimsController _controller;

    private readonly Guid _policyholderAId = Guid.NewGuid();
    private readonly Guid _policyholderBId = Guid.NewGuid();
    private readonly Guid _staffUserId = Guid.NewGuid();

    private Policy _policyA = null!;
    private Policy _policyB = null!;
    private DomainClaim _claimA = null!;
    private DomainClaim _claimB = null!;

    public ClaimRoleAuthorizationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClaimRoleAuthTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _claimRepository = new ClaimRepository(_context);
        _policyValidation = new PolicyValidationService(_context);
        _storageService = new FakeDocumentStorageService();
        _verificationClient = new FakeDocumentVerificationClient();

        _claimService = new ClaimService(_claimRepository, _storageService, _policyValidation, _verificationClient);
        _controller = new ClaimsController(_claimService, new StubNotificationOrchestrator(), new StubUserEmailResolver());

        SeedTestData();
    }

    private void SeedTestData()
    {
        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Comprehensive Auto",
            Description = "Auto insurance",
            BasePremiumRate = 500m
        };
        _context.PolicyTypes.Add(policyType);

        _policyA = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-A-100",
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
            PolicyNumber = "POL-B-200",
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

        _claimA = new DomainClaim
        {
            Id = Guid.NewGuid(),
            PolicyId = _policyA.Id,
            PolicyHolderId = _policyholderAId,
            ClaimNumber = "CLM-2026-0001",
            ClaimType = ClaimType.Auto,
            Description = "Front bumper damage",
            ClaimedAmount = 2500m,
            IncidentDate = DateTime.UtcNow.AddDays(-10),
            IncidentLocation = "Main St",
            Status = ClaimStatus.Draft
        };

        _claimB = new DomainClaim
        {
            Id = Guid.NewGuid(),
            PolicyId = _policyB.Id,
            PolicyHolderId = _policyholderBId,
            ClaimNumber = "CLM-2026-0002",
            ClaimType = ClaimType.Auto,
            Description = "Rear windshield break",
            ClaimedAmount = 1800m,
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            IncidentLocation = "2nd Ave",
            Status = ClaimStatus.Draft
        };

        _context.Policies.AddRange(_policyA, _policyB);
        _context.Claims.AddRange(_claimA, _claimB);
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

    // 1. Policyholder list returns only own claims
    [Fact]
    public async Task GetAllClaims_Policyholder_ReturnsOnlyOwnClaims()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetAllClaims();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var claims = Assert.IsAssignableFrom<List<ClaimSummaryDto>>(okResult.Value);

        Assert.Single(claims);
        Assert.Equal(_claimA.Id, claims[0].Id);
    }

    // 2. Policyholder cannot see another user's claim in list
    [Fact]
    public async Task GetAllClaims_Policyholder_AnotherUsersClaimsDoNotAppear()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetAllClaims();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var claims = Assert.IsAssignableFrom<List<ClaimSummaryDto>>(okResult.Value);

        Assert.DoesNotContain(claims, c => c.Id == _claimB.Id);
    }

    // 3. Policyholder can GET own claim by ID
    [Fact]
    public async Task GetClaim_Policyholder_CanAccessOwnClaim()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetClaim(_claimA.Id);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var claim = Assert.IsType<ClaimResponseDto>(okResult.Value);
        Assert.Equal(_claimA.Id, claim.Id);
    }

    // 4. Policyholder cannot GET another user's claim by ID
    [Fact]
    public async Task GetClaim_Policyholder_CannotAccessAnotherUsersClaim_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.GetClaim(_claimB.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 5. Policyholder creates claim on own policy -> success and owner matches JWT
    [Fact]
    public async Task CreateClaim_Policyholder_CreatesOnOwnPolicy_SuccessAndOwnerMatchesJwt()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var dto = new CreateClaimDto(
            PolicyId: _policyA.Id,
            ClaimType: ClaimType.Auto,
            IncidentDate: DateTime.UtcNow.AddDays(-2),
            IncidentLocation: "Elm Street",
            Description: "Hit a pothole",
            ClaimedAmount: 750m
        );

        var result = await _controller.CreateClaim(dto);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var created = Assert.IsType<ClaimResponseDto>(createdResult.Value);

        Assert.Equal(_policyA.Id, created.PolicyId);
        Assert.Equal(_policyholderAId, created.PolicyHolderId);
    }

    // 6. Policyholder cannot create claim using another user's policy
    [Fact]
    public async Task CreateClaim_Policyholder_CannotCreateOnAnotherUsersPolicy_Returns403()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var dto = new CreateClaimDto(
            PolicyId: _policyB.Id, // PolicyB belongs to PolicyholderB
            ClaimType: ClaimType.Auto,
            IncidentDate: DateTime.UtcNow.AddDays(-2),
            IncidentLocation: "Elm Street",
            Description: "Trying to claim against another user's policy",
            ClaimedAmount: 900m
        );

        var result = await _controller.CreateClaim(dto);

        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
    }

    // 7. Staff creates claim for another Policyholder's policy -> associates with policy owner, NOT staff ID
    [Theory]
    [InlineData("ClaimsAdjuster")]
    [InlineData("Underwriter")]
    [InlineData("Admin")]
    public async Task CreateClaim_Staff_AssociatesWithPolicyOwner_NotStaffUserId(string role)
    {
        SetUserContext(_staffUserId, role);

        var dto = new CreateClaimDto(
            PolicyId: _policyB.Id,
            ClaimType: ClaimType.Auto,
            IncidentDate: DateTime.UtcNow.AddDays(-3),
            IncidentLocation: "Staff Office",
            Description: "Staff assisted claim creation",
            ClaimedAmount: 1100m
        );

        var result = await _controller.CreateClaim(dto);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var created = Assert.IsType<ClaimResponseDto>(createdResult.Value);

        Assert.Equal(_policyB.Id, created.PolicyId);
        Assert.Equal(_policyholderBId, created.PolicyHolderId); // Associated with policy owner!
        Assert.NotEqual(_staffUserId, created.PolicyHolderId);  // NOT the staff user ID!
    }

    // 8. Staff can list all claims and view any claim
    [Theory]
    [InlineData("ClaimsAdjuster")]
    [InlineData("Underwriter")]
    [InlineData("Admin")]
    public async Task Staff_CanListAllClaimsAndAccessAnyClaim(string role)
    {
        SetUserContext(_staffUserId, role);

        var listResult = await _controller.GetAllClaims();
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var claims = Assert.IsAssignableFrom<List<ClaimSummaryDto>>(okList.Value);
        Assert.Equal(2, claims.Count);

        var getResultA = await _controller.GetClaim(_claimA.Id);
        Assert.IsType<OkObjectResult>(getResultA.Result);

        var getResultB = await _controller.GetClaim(_claimB.Id);
        Assert.IsType<OkObjectResult>(getResultB.Result);
    }

    // 9. Policyholder cannot update another user's claim
    [Fact]
    public async Task UpdateClaim_Policyholder_CannotUpdateAnotherUsersClaim_ReturnsForbid()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var dto = new UpdateClaimDto(
            Description: "Tampered description",
            IncidentLocation: null,
            ClaimedAmount: null,
            IncidentDate: null
        );

        var result = await _controller.UpdateClaim(_claimB.Id, dto);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // 10. Policyholder cannot delete another user's claim
    [Fact]
    public async Task DeleteClaim_Policyholder_CannotDeleteAnotherUsersClaim_Returns403()
    {
        SetUserContext(_policyholderAId, "Policyholder");

        var result = await _controller.DeleteClaim(_claimB.Id);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);
    }

    private class StubNotificationOrchestrator : INotificationOrchestrator
    {
        public Task<bool> NotifyAsync(
            string notificationKey,
            Guid userId,
            string recipientEmail,
            Guid? claimId,
            NotificationType type,
            string? claimNumber = null,
            Guid? payoutId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> NotifyAsync(
            Guid userId,
            string recipientEmail,
            Guid? claimId,
            NotificationType type,
            string? claimNumber = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }


    private class StubUserEmailResolver : IUserEmailResolver
    {
        public Task<string?> GetEmailAsync(Guid userId)
        {
            return Task.FromResult<string?>("user@example.com");
        }
    }
}
