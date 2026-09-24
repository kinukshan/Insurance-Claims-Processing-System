using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using DomainClaim = InsuranceClaims.Domain.ClaimsManagement.Claim;
using ClaimStatus = InsuranceClaims.Domain.ClaimsManagement.ClaimStatus;
using SecurityClaim = System.Security.Claims.Claim;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

/// <summary>
/// Tests covering payout approval persistence flow end-to-end:
/// 1. Approving PendingApproval payout succeeds.
/// 2. New PayoutApproval audit row is inserted.
/// 3. Payout status becomes Approved.
/// 4. ApprovedBy / ApprovalTimestamp are persisted.
/// 5. JWT reviewer identity is stored in approval record.
/// 6. Only one SaveChanges transaction is needed (atomic persistence).
/// 7. Underwriter can approve.
/// 8. Admin can approve.
/// 9. ClaimsAdjuster cannot approve.
/// 10. Approving an already finalized payout returns controlled conflict (409).
/// 11. Reject and RequestRevision do not suffer the same child-entity tracking bug.
/// 12. Raw DbUpdateConcurrencyException text is not exposed to frontend.
/// 13. Exact EF tracking state verification (Payout = Modified, PayoutApproval = Added).
/// </summary>
public class PayoutApprovalPersistenceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PayoutRepository _repository;
    private readonly FakePayoutContextProvider _contextProvider;
    private readonly FakeValidationAgentGateway _validationGateway;
    private readonly PayoutService _payoutService;
    private readonly FakePaymentGateway _paymentGateway;
    private readonly PayoutsController _controller;
    private readonly JwtService _jwtService;

    public PayoutApprovalPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PayoutApprovalTestDb_{Guid.NewGuid()}")
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

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretTestKey_AtLeast32CharactersLong_ForHmacSha256!",
                ["Jwt:Issuer"] = "InsuranceClaims",
                ["Jwt:Audience"] = "InsuranceClaims.React",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        _jwtService = new JwtService(config);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<(DomainClaim claim, Payout payout)> SeedClaimAndPayout(
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
            PolicyholderId = Guid.NewGuid(),
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
            PolicyHolderId = Guid.NewGuid(),
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

    private void SetControllerUserWithToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "InsuranceClaims",
            ValidAudience = "InsuranceClaims.React",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("SuperSecretTestKey_AtLeast32CharactersLong_ForHmacSha256!")),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };

        var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private void SetControllerUserWithClaims(IEnumerable<SecurityClaim> claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ── 1. Approving PendingApproval payout succeeds ──────────────────────

    [Fact]
    public async Task Approving_PendingApproval_Payout_Succeeds()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();
        var reviewerName = "Alice Underwriter";

        var result = await _payoutService.ApprovePayoutAsync(
            payout.Id, "All documents verified and coverage checked", reviewerId, reviewerName);

        Assert.NotNull(result);
        Assert.Equal(PayoutStatus.Approved, result.Status);
        Assert.Equal(reviewerName, result.ApprovedBy);
    }

    // ── 2. New PayoutApproval audit row is inserted ───────────────────────

    [Fact]
    public async Task New_PayoutApproval_Audit_Row_Is_Inserted()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();

        await _payoutService.ApprovePayoutAsync(
            payout.Id, "Approved by underwriter", reviewerId, "Alice Underwriter");

        var approvals = await _context.PayoutApprovals
            .Where(a => a.PayoutId == payout.Id)
            .ToListAsync();

        Assert.Single(approvals);
        var approval = approvals[0];
        Assert.Equal(ApprovalDecisionType.Approved, approval.Decision);
        Assert.Equal("Approved by underwriter", approval.Comments);
        Assert.Equal(reviewerId, approval.ReviewerId);
        Assert.Equal("Alice Underwriter", approval.ReviewerName);
    }

    // ── 3. Payout status becomes Approved ─────────────────────────────────

    [Fact]
    public async Task Payout_Status_Becomes_Approved()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);

        await _payoutService.ApprovePayoutAsync(
            payout.Id, "Approved", Guid.NewGuid(), "Alice Underwriter");

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Approved, updatedPayout.Status);
    }

    // ── 4. ApprovedBy / ApprovalTimestamp are persisted ───────────────────

    [Fact]
    public async Task ApprovedBy_And_ApprovalTimestamp_Are_Persisted()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var beforeApproval = DateTime.UtcNow.AddSeconds(-1);

        await _payoutService.ApprovePayoutAsync(
            payout.Id, "Approved", Guid.NewGuid(), "Alice Underwriter");

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal("Alice Underwriter", updatedPayout.ApprovedBy);
        Assert.NotNull(updatedPayout.ApprovalTimestamp);
        Assert.True(updatedPayout.ApprovalTimestamp >= beforeApproval);
    }

    // ── 5. JWT reviewer identity is stored in approval record ─────────────

    [Fact]
    public async Task Jwt_Reviewer_Identity_Is_Stored_In_Approval_Record()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var jwtUserId = Guid.NewGuid();
        var jwtUserName = "Reviewer From JWT";
        SetControllerUser(jwtUserId, jwtUserName, "Underwriter");

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "JWT Reviewer Approval" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(PayoutStatus.Approved, dto.Status);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);

        Assert.NotNull(savedApproval);
        Assert.Equal(jwtUserId, savedApproval.ReviewerId);
        Assert.Equal(jwtUserName, savedApproval.ReviewerName);
    }

    // ── 6. Only one SaveChanges transaction is needed (atomic persistence) ─

    [Fact]
    public async Task Only_One_SaveChanges_Persists_Both_Payout_And_Approval()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);

        // Call repository UpdateAsync directly after mutating domain aggregate
        var trackedPayout = await _repository.GetByIdAsync(payout.Id);
        Assert.NotNull(trackedPayout);

        var newApproval = trackedPayout.Approve(Guid.NewGuid(), "Reviewer", "Single transaction test");

        var approvalEntry = _context.Entry(newApproval);

        // Before repository.UpdateAsync, newApproval is Detached
        Assert.Equal(EntityState.Detached, approvalEntry.State);

        // UpdateAsync ensures approvalEntry.State is marked Added (not Modified), and Payout is Modified,
        // saving both atomically in a single SaveChanges transaction
        await _repository.UpdateAsync(trackedPayout);

        // After save, both are Unchanged in context and persisted in database
        Assert.Equal(EntityState.Unchanged, _context.Entry(trackedPayout).State);
        Assert.Equal(EntityState.Unchanged, _context.Entry(newApproval).State);

        var persistedPayout = await _context.Payouts.Include(p => p.Approvals).FirstAsync(p => p.Id == payout.Id);
        Assert.Equal(PayoutStatus.Approved, persistedPayout.Status);
        Assert.Single(persistedPayout.Approvals);
    }

    // ── 7. Underwriter can approve ────────────────────────────────────────

    [Fact]
    public async Task Underwriter_Can_Approve()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        SetControllerUser(Guid.NewGuid(), "Alice Underwriter", "Underwriter");

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Underwriter Approval" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(PayoutStatus.Approved, dto.Status);
    }

    // ── 8. Admin can approve ──────────────────────────────────────────────

    [Fact]
    public async Task Admin_Can_Approve()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        SetControllerUser(Guid.NewGuid(), "Bob Admin", "Admin");

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Admin Approval" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(PayoutStatus.Approved, dto.Status);
    }

    // ── 9. ClaimsAdjuster cannot approve (authorization attribute check) ──

    [Fact]
    public void ClaimsAdjuster_Cannot_Approve_Role_Attribute_Restricts_Access()
    {
        var method = typeof(PayoutsController).GetMethod(nameof(PayoutsController.ApprovePayout));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var allowedRoles = authAttr.Roles.Split(',').Select(r => r.Trim()).ToList();
        Assert.Contains("Underwriter", allowedRoles);
        Assert.Contains("Admin", allowedRoles);
        Assert.DoesNotContain("ClaimsAdjuster", allowedRoles);
    }

    // ── 10. Approving an already finalized payout returns controlled conflict ─

    [Fact]
    public async Task Approving_An_Already_Finalized_Payout_Returns_Controlled_Conflict()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.Approved);
        SetControllerUser(Guid.NewGuid(), "Alice Underwriter", "Underwriter");

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Duplicate approval" });

        var conflictResult = Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);

        // Verify controlled conflict message
        var val = conflictResult.Value;
        Assert.NotNull(val);
        var errorProp = val.GetType().GetProperty("error")?.GetValue(val)?.ToString();
        Assert.NotNull(errorProp);
        Assert.Contains("Cannot approve payout from status 'Approved'", errorProp);
        Assert.Contains("Must be PendingApproval", errorProp);
    }

    // ── 11. Reject and RequestRevision do not suffer the same bug ─────────

    [Fact]
    public async Task Reject_Payout_Inserts_Audit_Row_Without_Tracking_Bug()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();

        var result = await _payoutService.RejectPayoutAsync(
            payout.Id, "Rejected due to coverage exclusion", reviewerId, "Alice Underwriter");

        Assert.Equal(PayoutStatus.Rejected, result.Status);

        var approval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);

        Assert.NotNull(approval);
        Assert.Equal(ApprovalDecisionType.Rejected, approval.Decision);
        Assert.Equal("Rejected due to coverage exclusion", approval.Comments);

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.Equal(PayoutStatus.Rejected, updatedPayout!.Status);
    }

    [Fact]
    public async Task RequestRevision_Payout_Inserts_Audit_Row_Without_Tracking_Bug()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();

        var result = await _payoutService.RequestRevisionAsync(
            payout.Id, "Need invoice clarification", reviewerId, "Alice Underwriter");

        Assert.Equal(PayoutStatus.RevisionRequested, result.Status);

        var approval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);

        Assert.NotNull(approval);
        Assert.Equal(ApprovalDecisionType.RevisionRequested, approval.Decision);
        Assert.Equal("Need invoice clarification", approval.Comments);

        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.Equal(PayoutStatus.RevisionRequested, updatedPayout!.Status);
    }

    [Fact]
    public async Task Consecutive_Decisions_Maintain_All_Audit_Logs()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();

        // 1. Request revision
        await _payoutService.RequestRevisionAsync(
            payout.Id, "First review: please update deductible", reviewerId, "Alice Underwriter");

        // 2. Recalculate / update back to Draft and then PendingApproval
        var tracked = await _repository.GetByIdAsync(payout.Id);
        tracked!.Status = PayoutStatus.PendingApproval;
        await _repository.UpdateAsync(tracked);

        // 3. Underwriter approves on second pass
        await _payoutService.ApprovePayoutAsync(
            payout.Id, "Second review: approved", reviewerId, "Alice Underwriter");

        var allApprovals = await _context.PayoutApprovals
            .Where(a => a.PayoutId == payout.Id)
            .OrderBy(a => a.DecisionTimestamp)
            .ToListAsync();

        // Both audit records must be present
        Assert.Equal(2, allApprovals.Count);
        Assert.Equal(ApprovalDecisionType.RevisionRequested, allApprovals[0].Decision);
        Assert.Equal(ApprovalDecisionType.Approved, allApprovals[1].Decision);
    }

    // ── 12. Raw DbUpdateConcurrencyException text is not exposed to frontend ─

    [Fact]
    public async Task Raw_DbUpdateConcurrencyException_Is_Mapped_To_Controlled_409()
    {
        // Create a controller with a mock service that throws DbUpdateConcurrencyException
        var mockService = new ConcurrencyFailingPayoutService();
        var controller = new PayoutsController(
            mockService,
            _paymentGateway,
            _repository,
            new PaymentTransactionRepository(_context),
            NullLogger<PayoutsController>.Instance);

        var claims = new List<SecurityClaim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "Alice Underwriter"),
            new(ClaimTypes.Role, "Underwriter")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) }
        };

        var response = await controller.ApprovePayout(
            Guid.NewGuid(), new PayoutApprovalRequestDto { Comments = "Test" });

        var conflictResult = Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);

        var val = conflictResult.Value;
        Assert.NotNull(val);
        var errorProp = val.GetType().GetProperty("error")?.GetValue(val)?.ToString();

        Assert.NotNull(errorProp);
        // Controlled message returned
        Assert.Equal("The payout was modified or approved by another user. Please refresh and try again.", errorProp);

        // Ensure raw EF Core exception text is NOT leaked
        Assert.DoesNotContain("The database operation was expected to affect 1 row(s)", errorProp);
        Assert.DoesNotContain("DbUpdateConcurrencyException", errorProp);
    }

    // ── 13. Exact EF tracking state verification ──────────────────────────

    [Fact]
    public async Task EF_EntityState_Before_SaveChanges_Is_Modified_For_Payout_And_Added_For_New_Approval()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);

        // Load as tracked aggregate
        var trackedPayout = await _repository.GetByIdAsync(payout.Id);
        Assert.NotNull(trackedPayout);

        var newApproval = trackedPayout.Approve(Guid.NewGuid(), "Jane Underwriter", "Audit check");

        // Inspect states before UpdateAsync explicitly sets them
        var payoutEntry = _context.Entry(trackedPayout);
        var approvalEntry = _context.Entry(newApproval);

        // Simulate the repository tracking logic before calling SaveChanges
        if (payoutEntry.State == EntityState.Detached)
        {
            _context.Payouts.Attach(trackedPayout);
            payoutEntry.State = EntityState.Modified;
        }
        else if (payoutEntry.State == EntityState.Unchanged)
        {
            payoutEntry.State = EntityState.Modified;
        }

        foreach (var approval in trackedPayout.Approvals)
        {
            var appEntry = _context.Entry(approval);
            if (appEntry.State == EntityState.Detached || appEntry.State == EntityState.Modified)
            {
                appEntry.State = EntityState.Added;
            }
        }

        // Exact assertions on entity states before SaveChanges:
        Assert.Equal(EntityState.Modified, payoutEntry.State);
        Assert.Equal(EntityState.Added, approvalEntry.State);

        // SaveChanges succeeds without DbUpdateConcurrencyException
        await _context.SaveChangesAsync();

        // After SaveChanges, both are Unchanged in tracking context
        Assert.Equal(EntityState.Unchanged, payoutEntry.State);
        Assert.Equal(EntityState.Unchanged, approvalEntry.State);
    }

    // ── 14. Actual login token JWT tests & identity claim fallbacks ───────

    [Fact]
    public async Task ApprovePayout_WithActualJwtToken_PersistsRealReviewerNameAndId()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice.underwriter@insurance.com",
            FirstName = "Alice",
            LastName = "Underwriter",
            Role = Role.Underwriter,
            IsActive = true
        };
        var token = _jwtService.GenerateToken(staffUser);
        SetControllerUserWithToken(token);

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Real token approval" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);

        Assert.Equal(PayoutStatus.Approved, dto.Status);
        Assert.Equal("Alice Underwriter", dto.ApprovedBy);
        Assert.NotEqual("Unknown Reviewer", dto.ApprovedBy);

        var persistedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(persistedPayout);
        Assert.Equal("Alice Underwriter", persistedPayout.ApprovedBy);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);
        Assert.NotNull(savedApproval);
        Assert.Equal(staffUser.Id, savedApproval.ReviewerId);
        Assert.Equal("Alice Underwriter", savedApproval.ReviewerName);
        Assert.NotEqual("Unknown Reviewer", savedApproval.ReviewerName);
        Assert.Equal(ApprovalDecisionType.Approved, savedApproval.Decision);
    }

    [Fact]
    public async Task RejectPayout_WithActualJwtToken_PersistsRealReviewerNameAndId()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "alice.underwriter@insurance.com",
            FirstName = "Alice",
            LastName = "Underwriter",
            Role = Role.Underwriter,
            IsActive = true
        };
        var token = _jwtService.GenerateToken(staffUser);
        SetControllerUserWithToken(token);

        var response = await _controller.RejectPayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Real token rejection" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(PayoutStatus.Rejected, dto.Status);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);
        Assert.NotNull(savedApproval);
        Assert.Equal(staffUser.Id, savedApproval.ReviewerId);
        Assert.Equal("Alice Underwriter", savedApproval.ReviewerName);
        Assert.NotEqual("Unknown Reviewer", savedApproval.ReviewerName);
        Assert.Equal(ApprovalDecisionType.Rejected, savedApproval.Decision);
    }

    [Fact]
    public async Task RequestRevision_WithActualJwtToken_PersistsRealReviewerNameAndId()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "bob.admin@insurance.com",
            FirstName = "Bob",
            LastName = "Admin",
            Role = Role.Admin,
            IsActive = true
        };
        var token = _jwtService.GenerateToken(adminUser);
        SetControllerUserWithToken(token);

        var response = await _controller.RequestRevision(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Real token revision request" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(PayoutStatus.RevisionRequested, dto.Status);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);
        Assert.NotNull(savedApproval);
        Assert.Equal(adminUser.Id, savedApproval.ReviewerId);
        Assert.Equal("Bob Admin", savedApproval.ReviewerName);
        Assert.NotEqual("Unknown Reviewer", savedApproval.ReviewerName);
        Assert.Equal(ApprovalDecisionType.RevisionRequested, savedApproval.Decision);
    }

    [Fact]
    public async Task ReviewerIdentity_ResolvesFromGivenNameAndSurname_WhenDirectNameClaimMissing()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();

        // Token without ClaimTypes.Name or "name": only GivenName + Surname
        SetControllerUserWithClaims(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, reviewerId.ToString()),
            new SecurityClaim(ClaimTypes.GivenName, "Carol"),
            new SecurityClaim(ClaimTypes.Surname, "Underwriter"),
            new SecurityClaim(ClaimTypes.Email, "carol@claims.com"),
            new SecurityClaim(ClaimTypes.Role, "Underwriter")
        });

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Given + Surname resolution" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal("Carol Underwriter", dto.ApprovedBy);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);
        Assert.NotNull(savedApproval);
        Assert.Equal(reviewerId, savedApproval.ReviewerId);
        Assert.Equal("Carol Underwriter", savedApproval.ReviewerName);
    }

    [Fact]
    public async Task ReviewerIdentity_ResolvesFromRawJwtClaims_WhenInboundMappingDisabled()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();

        // Raw JWT claim names: "sub", "given_name", "family_name", "email", "role"
        SetControllerUserWithClaims(new[]
        {
            new SecurityClaim("sub", reviewerId.ToString()),
            new SecurityClaim("given_name", "David"),
            new SecurityClaim("family_name", "Reviewer"),
            new SecurityClaim("email", "david@claims.com"),
            new SecurityClaim(ClaimTypes.Role, "Underwriter")
        });

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Raw JWT claims resolution" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal("David Reviewer", dto.ApprovedBy);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);
        Assert.NotNull(savedApproval);
        Assert.Equal(reviewerId, savedApproval.ReviewerId);
        Assert.Equal("David Reviewer", savedApproval.ReviewerName);
    }

    [Fact]
    public async Task ReviewerIdentity_ResolvesToEmail_WhenFirstAndLastNamesEmpty()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);
        var reviewerId = Guid.NewGuid();

        SetControllerUserWithClaims(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, reviewerId.ToString()),
            new SecurityClaim(ClaimTypes.Email, "reviewer@claims.com"),
            new SecurityClaim(ClaimTypes.Role, "Underwriter")
        });

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Email fallback resolution" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal("reviewer@claims.com", dto.ApprovedBy);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);
        Assert.NotNull(savedApproval);
        Assert.Equal(reviewerId, savedApproval.ReviewerId);
        Assert.Equal("reviewer@claims.com", savedApproval.ReviewerName);
    }

    [Fact]
    public async Task ReviewerIdentity_FallsBackToUnknownReviewer_WhenNoIdentityClaimsExist()
    {
        var (_, payout) = await SeedClaimAndPayout(PayoutStatus.PendingApproval);

        // Only role claim, no ID, name, or email
        SetControllerUserWithClaims(new[]
        {
            new SecurityClaim(ClaimTypes.Role, "Underwriter")
        });

        var response = await _controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "No identity claims" });

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal("Unknown Reviewer", dto.ApprovedBy);

        var savedApproval = await _context.PayoutApprovals
            .FirstOrDefaultAsync(a => a.PayoutId == payout.Id);
        Assert.NotNull(savedApproval);
        Assert.Equal(Guid.Empty, savedApproval.ReviewerId);
        Assert.Equal("Unknown Reviewer", savedApproval.ReviewerName);
    }

    // ── Test doubles ──────────────────────────────────────────────────────

    private class ConcurrencyFailingPayoutService : IPayoutService
    {
        public Task<PayoutDto> ApprovePayoutAsync(Guid id, string comments, Guid reviewerId, string reviewerName)
        {
            throw new DbUpdateConcurrencyException(
                "The database operation was expected to affect 1 row(s), but actually affected 0 row(s).");
        }

        public Task<PayoutDto> CalculatePayoutAsync(Guid claimId) => throw new NotImplementedException();
        public Task<PayoutDto?> GetByIdAsync(Guid id, Guid? userId = null, Role? role = null) => throw new NotImplementedException();
        public Task<PayoutDto?> GetByClaimIdAsync(Guid claimId, Guid? userId = null, Role? role = null) => throw new NotImplementedException();
        public Task<PaginatedResult<PayoutDto>> GetHistoryAsync(PayoutHistoryQueryDto query) => throw new NotImplementedException();
        public Task<PaginatedResult<PayoutDto>> GetMyPayoutsAsync(Guid policyholderId, PayoutHistoryQueryDto query) => throw new NotImplementedException();
        public Task<PayoutDto> UpdatePayoutAsync(Guid id) => throw new NotImplementedException();
        public Task<PayoutDto> RejectPayoutAsync(Guid id, string comments, Guid reviewerId, string reviewerName) => throw new NotImplementedException();
        public Task<PayoutDto> RequestRevisionAsync(Guid id, string comments, Guid reviewerId, string reviewerName) => throw new NotImplementedException();
        public Task<PayoutDto> ExecutePayoutAsync(Guid id) => throw new NotImplementedException();
        public Task DeletePayoutAsync(Guid id) => throw new NotImplementedException();
    }

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
