using System.Reflection;
using System.Security.Claims;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainClaim = InsuranceClaims.Domain.ClaimsManagement.Claim;
using ClaimStatus = InsuranceClaims.Domain.ClaimsManagement.ClaimStatus;
using SecurityClaim = System.Security.Claims.Claim;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

/// <summary>
/// Tests covering Policyholder vs Staff role authorization and ownership enforcement in Payouts module.
/// </summary>
public class PayoutRoleAuthorizationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PayoutRepository _repository;
    private readonly FakePayoutContextProvider _contextProvider;
    private readonly FakeValidationAgentGateway _validationGateway;
    private readonly PayoutService _payoutService;
    private readonly FakePaymentGateway _paymentGateway;
    private readonly PayoutsController _controller;

    public PayoutRoleAuthorizationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PayoutRoleAuthTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new PayoutRepository(_context);
        _contextProvider = new FakePayoutContextProvider();
        _validationGateway = new FakeValidationAgentGateway();
        _paymentGateway = new FakePaymentGateway();

        _payoutService = new PayoutService(_repository, _contextProvider, _validationGateway);
        var transactionRepo = new PaymentTransactionRepository(_context);
        _controller = new PayoutsController(
            _payoutService,
            _paymentGateway,
            _repository,
            transactionRepo,
            NullLogger<PayoutsController>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<(DomainClaim claim, Payout payout)> SeedClaimAndPayout(
        Guid policyholderId,
        PayoutStatus initialStatus = PayoutStatus.PendingApproval)
    {
        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Comprehensive Auto",
            Description = "Full motor coverage"
        };
        _context.PolicyTypes.Add(policyType);

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = $"POL-{Guid.NewGuid():N}"[..12].ToUpper(),
            PolicyholderId = policyholderId,
            PolicyTypeId = policyType.Id,
            PolicyType = policyType,
            CoverageLimit = 25000m,
            Deductible = 500m,
            Premium = 1200m,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            Status = PolicyStatus.Active
        };
        _context.Policies.Add(policy);

        var claim = new DomainClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = $"CLM-{Guid.NewGuid():N}"[..12].ToUpper(),
            PolicyId = policy.Id,
            Policy = policy,
            PolicyHolderId = policyholderId,
            ClaimedAmount = 5000m,
            IncidentDate = DateTime.UtcNow.AddDays(-10),
            Description = "Front collision damage",
            Status = ClaimStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };
        _context.Claims.Add(claim);

        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            Claim = claim,
            ApprovedClaimAmount = 5000m,
            CoverageLimit = 25000m,
            Deductible = 500m,
            ProposedPayout = 4500m,
            FinalPayout = 4500m,
            Status = initialStatus,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payouts.Add(payout);

        await _context.SaveChangesAsync();
        return (claim, payout);
    }

    private void SetControllerUser(Guid userId, string userName, string role)
    {
        var claims = new List<SecurityClaim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ── 1. Policyholder sees only their own payouts in GET /api/payouts/my ──

    [Fact]
    public async Task Policyholder_GetMyPayouts_ReturnsOnlyOwnPayouts()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var (_, payoutA) = await SeedClaimAndPayout(userA);
        var (_, payoutB) = await SeedClaimAndPayout(userB);

        SetControllerUser(userA, "Policyholder A", "Policyholder");

        var response = await _controller.GetMyPayouts();
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var pagedResult = Assert.IsType<PaginatedResult<PayoutDto>>(okResult.Value);

        Assert.Equal(1, pagedResult.TotalCount);
        Assert.Single(pagedResult.Items);
        Assert.Equal(payoutA.Id, pagedResult.Items[0].Id);
        Assert.DoesNotContain(pagedResult.Items, p => p.Id == payoutB.Id);
    }

    // ── 2. Policyholder cannot access another user's payout by ID ──────────

    [Fact]
    public async Task Policyholder_GetById_OwnPayout_ReturnsOk()
    {
        var userA = Guid.NewGuid();
        var (_, payoutA) = await SeedClaimAndPayout(userA);

        SetControllerUser(userA, "Policyholder A", "Policyholder");

        var response = await _controller.GetById(payoutA.Id);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(payoutA.Id, dto.Id);
    }

    [Fact]
    public async Task Policyholder_GetById_OtherUserPayout_ReturnsForbidden()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var (_, payoutB) = await SeedClaimAndPayout(userB);

        // User A tries to get User B's payout
        SetControllerUser(userA, "Policyholder A", "Policyholder");

        var response = await _controller.GetById(payoutB.Id);
        Assert.IsType<ForbidResult>(response.Result);
    }

    // ── 3. Policyholder cannot access another user's payout by Claim ID ────

    [Fact]
    public async Task Policyholder_GetByClaimId_OwnClaim_ReturnsOk()
    {
        var userA = Guid.NewGuid();
        var (claimA, payoutA) = await SeedClaimAndPayout(userA);

        SetControllerUser(userA, "Policyholder A", "Policyholder");

        var response = await _controller.GetByClaimId(claimA.Id);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(payoutA.Id, dto.Id);
    }

    [Fact]
    public async Task Policyholder_GetByClaimId_OtherUserClaim_ReturnsForbidden()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var (claimB, _) = await SeedClaimAndPayout(userB);

        // User A tries to get User B's payout via claimId
        SetControllerUser(userA, "Policyholder A", "Policyholder");

        var response = await _controller.GetByClaimId(claimB.Id);
        Assert.IsType<ForbidResult>(response.Result);
    }

    // ── 4. Staff can access any payout by ID and Claim ID ──────────────────

    [Theory]
    [InlineData("ClaimsAdjuster")]
    [InlineData("Underwriter")]
    [InlineData("Admin")]
    public async Task Staff_Can_Access_Any_Payout_By_Id_And_ClaimId(string staffRole)
    {
        var policyholder = Guid.NewGuid();
        var (claim, payout) = await SeedClaimAndPayout(policyholder);

        SetControllerUser(Guid.NewGuid(), "Staff Member", staffRole);

        // By ID
        var idResponse = await _controller.GetById(payout.Id);
        var okIdResult = Assert.IsType<OkObjectResult>(idResponse.Result);
        Assert.NotNull(okIdResult.Value);

        // By Claim ID
        var claimResponse = await _controller.GetByClaimId(claim.Id);
        var okClaimResult = Assert.IsType<OkObjectResult>(claimResponse.Result);
        Assert.NotNull(okClaimResult.Value);
    }

    // ── 5. Staff can view all payouts in GET /api/payouts/history ───────────

    [Fact]
    public async Task Staff_GetHistory_ReturnsAllPayouts()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await SeedClaimAndPayout(userA);
        await SeedClaimAndPayout(userB);

        SetControllerUser(Guid.NewGuid(), "Claims Adjuster", "ClaimsAdjuster");

        var response = await _controller.GetHistory();
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var pagedResult = Assert.IsType<PaginatedResult<PayoutDto>>(okResult.Value);

        Assert.Equal(2, pagedResult.TotalCount);
    }

    // ── 6. Verification of Route & Action Authorization Attributes ─────────

    [Fact]
    public void Controller_Class_Is_Authorized_For_All_Authenticated_Users()
    {
        var authAttr = typeof(PayoutsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        // Should not have restrictive role constraint at class level
        Assert.True(string.IsNullOrEmpty(authAttr.Roles));
    }

    [Fact]
    public void CalculatePayout_Is_Restricted_To_Staff()
    {
        var method = typeof(PayoutsController).GetMethod(nameof(PayoutsController.CalculatePayout));
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var allowedRoles = authAttr.Roles.Split(',').Select(r => r.Trim()).ToList();
        Assert.Contains("ClaimsAdjuster", allowedRoles);
        Assert.Contains("Underwriter", allowedRoles);
        Assert.Contains("Admin", allowedRoles);
        Assert.DoesNotContain("Policyholder", allowedRoles);
    }

    [Fact]
    public void History_Is_Restricted_To_Staff()
    {
        var method = typeof(PayoutsController).GetMethod(nameof(PayoutsController.GetHistory));
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var allowedRoles = authAttr.Roles.Split(',').Select(r => r.Trim()).ToList();
        Assert.Contains("ClaimsAdjuster", allowedRoles);
        Assert.Contains("Underwriter", allowedRoles);
        Assert.Contains("Admin", allowedRoles);
        Assert.DoesNotContain("Policyholder", allowedRoles);
    }

    [Fact]
    public void Approve_Reject_RequestRevision_Are_Restricted_To_Underwriter_And_Admin()
    {
        var approveMethod = typeof(PayoutsController).GetMethod(nameof(PayoutsController.ApprovePayout));
        var rejectMethod = typeof(PayoutsController).GetMethod(nameof(PayoutsController.RejectPayout));
        var revisionMethod = typeof(PayoutsController).GetMethod(nameof(PayoutsController.RequestRevision));

        foreach (var method in new[] { approveMethod, rejectMethod, revisionMethod })
        {
            Assert.NotNull(method);
            var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.NotNull(authAttr.Roles);

            var allowedRoles = authAttr.Roles.Split(',').Select(r => r.Trim()).ToList();
            Assert.Contains("Underwriter", allowedRoles);
            Assert.Contains("Admin", allowedRoles);
            Assert.DoesNotContain("ClaimsAdjuster", allowedRoles);
            Assert.DoesNotContain("Policyholder", allowedRoles);
        }
    }

    [Fact]
    public void ExecutePayout_Is_Restricted_To_Admin_Only()
    {
        var method = typeof(PayoutsController).GetMethod(nameof(PayoutsController.ExecutePayout));
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var allowedRoles = authAttr.Roles.Split(',').Select(r => r.Trim()).ToList();
        Assert.Contains("Admin", allowedRoles);
        Assert.DoesNotContain("Underwriter", allowedRoles);
        Assert.DoesNotContain("ClaimsAdjuster", allowedRoles);
        Assert.DoesNotContain("Policyholder", allowedRoles);
    }

    // ── Test doubles ──────────────────────────────────────────────────────

    private class FakePayoutContextProvider : IPayoutContextProvider
    {
        public Task<PayoutContext?> GetPayoutContextAsync(Guid claimId)
        {
            return Task.FromResult<PayoutContext?>(new PayoutContext
            {
                ClaimId = claimId,
                ApprovedClaimAmount = 5000m,
                PolicyId = Guid.NewGuid(),
                PolicyType = "Comprehensive Auto",
                ClaimType = "Auto",
                CoverageLimit = 25000m,
                Deductible = 500m
            });
        }
    }

    private class FakeValidationAgentGateway : IPayoutValidationAgentGateway
    {
        public Task<PayoutValidationResult> ValidatePayoutProposalAsync(PayoutValidationRequest request)
        {
            return Task.FromResult(new PayoutValidationResult
            {
                Valid = true,
                RequiresHumanApproval = true,
                Summary = "Valid claim"
            });
        }
    }

    private class FakePaymentGateway : InsuranceClaims.Application.PayoutProcessing.Interfaces.IPaymentGateway
    {
        public Task<PaymentGatewayResult> CreatePayoutAsync(
            PaymentGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PaymentGatewayResult
            {
                Success = true,
                Provider = "Mock",
                ProviderTransactionId = $"TX-{Guid.NewGuid():N}"[..16].ToUpper(),
                ProviderStatus = "succeeded",
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(
            string providerTransactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PaymentGatewayStatusResult?>(new PaymentGatewayStatusResult
            {
                ProviderTransactionId = providerTransactionId,
                Status = "succeeded",
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<bool> ValidateWebhookAsync(
            string payload,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
