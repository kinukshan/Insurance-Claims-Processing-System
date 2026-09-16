using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Domain.ClaimsManagement;

namespace InsuranceClaims.UnitTests.ClaimsManagement;

/// <summary>
/// Unit tests for ClaimService — validates CRUD, ownership,
/// status transitions, and business operations.
/// </summary>
public class ClaimServiceTests
{
    private readonly ClaimService _service;
    private readonly IClaimRepository _repository;
    private readonly IDocumentStorageService _storage;
    private readonly IPolicyValidationService _policyValidation;
    private readonly IDocumentVerificationClient _verificationClient;

    // Test user IDs
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PolicyId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public ClaimServiceTests()
    {
        _repository = new FakeClaimRepository();
        _storage = new FakeDocumentStorageService();
        _policyValidation = new FakePolicyValidationService();
        _verificationClient = new FakeDocumentVerificationClient();
        _service = new ClaimService(_repository, _storage, _policyValidation, _verificationClient);
    }

    // ── CREATE ──

    [Fact]
    public async Task CreateClaim_WithValidData_ReturnsClaim()
    {
        var dto = MakeCreateDto();
        var result = await _service.CreateClaimAsync(UserId, dto);

        Assert.NotNull(result);
        Assert.Equal("Auto", result.ClaimType);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(UserId, result.PolicyHolderId);
        Assert.True(result.ClaimedAmount > 0);
    }

    [Fact]
    public async Task CreateClaim_WithEmptyDescription_Throws()
    {
        var dto = MakeCreateDto() with { Description = "" };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateClaimAsync(UserId, dto));
    }

    [Fact]
    public async Task CreateClaim_WithNegativeAmount_Throws()
    {
        var dto = MakeCreateDto() with { ClaimedAmount = -100 };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateClaimAsync(UserId, dto));
    }

    [Fact]
    public async Task CreateClaim_WithFutureDate_Throws()
    {
        var dto = MakeCreateDto() with { IncidentDate = DateTime.UtcNow.AddDays(5) };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateClaimAsync(UserId, dto));
    }

    [Fact]
    public async Task CreateClaim_WithEmptyLocation_Throws()
    {
        var dto = MakeCreateDto() with { IncidentLocation = "" };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateClaimAsync(UserId, dto));
    }

    // ── READ (Ownership) ──

    [Fact]
    public async Task GetClaim_AsOwner_ReturnsClaim()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.GetClaimAsync(created.Id, UserId);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
    }

    [Fact]
    public async Task GetClaim_AsNonOwner_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetClaimAsync(created.Id, OtherUserId));
    }

    [Fact]
    public async Task GetClaim_NonExistent_ReturnsNull()
    {
        var result = await _service.GetClaimAsync(Guid.NewGuid(), UserId);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMyClaims_ReturnsOnlyOwnClaims()
    {
        await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.CreateClaimAsync(OtherUserId, MakeCreateDto());

        var myClaims = await _service.GetMyClaimsAsync(UserId);
        Assert.Equal(2, myClaims.Count);
    }

    // ── UPDATE ──

    [Fact]
    public async Task UpdateClaim_DraftClaim_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var updateDto = new UpdateClaimDto("Updated description", null, 2000m, null);
        var result = await _service.UpdateClaimAsync(created.Id, UserId, updateDto);

        Assert.NotNull(result);
        Assert.Equal("Updated description", result!.Description);
        Assert.Equal(2000m, result.ClaimedAmount);
    }

    [Fact]
    public async Task UpdateClaim_SubmittedClaim_Throws()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);

        var updateDto = new UpdateClaimDto("Updated", null, null, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateClaimAsync(created.Id, UserId, updateDto));
    }

    [Fact]
    public async Task UpdateClaim_AsNonOwner_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var updateDto = new UpdateClaimDto("Hacked", null, null, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpdateClaimAsync(created.Id, OtherUserId, updateDto));
    }

    // ── DELETE (Withdraw) ──

    [Fact]
    public async Task DeleteClaim_DraftClaim_SetsWithdrawn()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.DeleteClaimAsync(created.Id, UserId);

        Assert.True(result);

        // Verify the status is Withdrawn, not hard-deleted
        var claim = await _service.GetClaimAsync(created.Id, UserId);
        Assert.Equal("Withdrawn", claim!.Status);
    }

    [Fact]
    public async Task DeleteClaim_SubmittedClaim_Throws()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteClaimAsync(created.Id, UserId));
    }

    [Fact]
    public async Task DeleteClaim_AsNonOwner_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteClaimAsync(created.Id, OtherUserId));
    }

    // ── STATUS TRANSITIONS ──

    [Fact]
    public async Task SubmitClaim_DraftClaim_SetsSubmittedStatus()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.SubmitClaimAsync(created.Id, UserId);

        Assert.NotNull(result);
        Assert.Equal("Submitted", result!.Status);
        Assert.NotNull(result.SubmittedAt);
    }

    [Fact]
    public async Task SubmitClaim_AlreadySubmitted_Throws()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubmitClaimAsync(created.Id, UserId));
    }

    // ── COVERAGE VALIDATION ──

    [Fact]
    public async Task ValidateCoverage_ReturnsResult()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.ValidateCoverageAsync(created.Id, UserId);

        Assert.True(result.IsValid);
        Assert.True(result.IsCovered);
    }

    // ── DOCUMENT VERIFICATION ──

    [Fact]
    public async Task VerifyDocuments_ReturnsAgentResult()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.VerifyDocumentsAsync(created.Id, UserId);

        Assert.NotNull(result);
    }

    // ── Helpers ──

    private static CreateClaimDto MakeCreateDto()
    {
        return new CreateClaimDto(
            PolicyId,
            ClaimType.Auto,
            DateTime.UtcNow.AddDays(-7),
            "123 Test Street",
            "Rear-end collision at an intersection",
            5000m
        );
    }
}

// ═══════════════════════════════════════════════════════
// Fakes (in-memory test doubles)
// ═══════════════════════════════════════════════════════

internal class FakeClaimRepository : IClaimRepository
{
    private readonly List<Claim> _claims = new();

    public Task<Claim?> GetByIdAsync(Guid id) =>
        Task.FromResult(_claims.FirstOrDefault(c => c.Id == id));

    public Task<Claim?> GetByIdWithDocumentsAsync(Guid id) =>
        Task.FromResult(_claims.FirstOrDefault(c => c.Id == id));

    public Task<List<Claim>> GetByPolicyHolderIdAsync(Guid policyHolderId) =>
        Task.FromResult(_claims.Where(c => c.PolicyHolderId == policyHolderId).ToList());

    public Task<List<Claim>> GetAllAsync(string? statusFilter = null, string? searchTerm = null) =>
        Task.FromResult(_claims.ToList());

    public Task<Claim> AddAsync(Claim claim)
    {
        claim.CreatedAt = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;
        _claims.Add(claim);
        return Task.FromResult(claim);
    }

    public Task<Claim> UpdateAsync(Claim claim)
    {
        claim.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(claim);
    }

    public Task DeleteAsync(Claim claim)
    {
        _claims.Remove(claim);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id) =>
        Task.FromResult(_claims.Any(c => c.Id == id));

    public Task<string> GenerateClaimNumberAsync() =>
        Task.FromResult($"CLM-TEST-{_claims.Count + 1:D4}");
}

internal class FakeDocumentStorageService : IDocumentStorageService
{
    public Task<string> UploadAsync(string fileName, string contentType, Stream fileStream) =>
        Task.FromResult($"/uploads/fake_{fileName}");

    public Task<bool> DeleteAsync(string fileUrl) =>
        Task.FromResult(true);
}

internal class FakePolicyValidationService : IPolicyValidationService
{
    public Task<CoverageValidationResultDto> ValidateCoverageAsync(Guid policyId, string claimType, decimal claimedAmount) =>
        Task.FromResult(new CoverageValidationResultDto(true, true, 100000m, 500m, claimType, new List<string>()));

    public Task<bool> IsPolicyActiveAsync(Guid policyId) =>
        Task.FromResult(true);
}

internal class FakeDocumentVerificationClient : IDocumentVerificationClient
{
    public Task<DocumentVerificationResultDto> VerifyDocumentsAsync(
        Guid claimId, string claimType, List<ClaimDocumentDto> documents,
        DateTime incidentDate, decimal claimedAmount) =>
        Task.FromResult(new DocumentVerificationResultDto(
            true, new List<string>(), new List<DocumentInconsistencyDto>(), new List<string>()));
}
