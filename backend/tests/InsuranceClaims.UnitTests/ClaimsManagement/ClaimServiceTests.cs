using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.Users;
using Xunit;

namespace InsuranceClaims.UnitTests.ClaimsManagement;

public class ClaimServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid PolicyId = Guid.NewGuid();

    private readonly FakeClaimRepository _claimRepository;
    private readonly FakeDocumentStorageService _storageService;
    private readonly FakePolicyValidationService _policyValidation;
    private readonly FakeDocumentVerificationClient _verificationClient;
    private readonly ClaimService _service;

    public ClaimServiceTests()
    {
        _claimRepository = new FakeClaimRepository();
        _storageService = new FakeDocumentStorageService();
        _policyValidation = new FakePolicyValidationService();
        _verificationClient = new FakeDocumentVerificationClient();
        _service = new ClaimService(_claimRepository, _storageService, _policyValidation, _verificationClient);
    }

    // ── CREATE ──

    [Fact]
    public async Task CreateClaim_ValidDto_ReturnsCreatedClaimWithDraftStatus()
    {
        var dto = MakeCreateDto();
        var result = await _service.CreateClaimAsync(UserId, dto);

        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(dto.ClaimType.ToString(), result.ClaimType);
        Assert.Equal(dto.ClaimedAmount, result.ClaimedAmount);
        Assert.Equal(UserId, result.PolicyHolderId);
        Assert.StartsWith("CLM-", result.ClaimNumber);
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
        var dto = MakeCreateDto() with { IncidentDate = DateTime.UtcNow.AddDays(1) };
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

    // ── READ ──

    [Fact]
    public async Task GetClaim_AsOwner_ReturnsClaimWithDocuments()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.GetClaimAsync(created.Id, UserId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
        Assert.Equal(created.ClaimNumber, result.ClaimNumber);
    }

    [Fact]
    public async Task GetClaim_AsOtherPolicyholder_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetClaimAsync(created.Id, OtherUserId, Role.Policyholder));
    }

    [Fact]
    public async Task GetClaim_AsClaimsAdjuster_CanAccessOtherUserClaim()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.GetClaimAsync(created.Id, OtherUserId, Role.ClaimsAdjuster);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
    }

    [Fact]
    public async Task GetClaim_AsUnderwriter_CanAccessOtherUserClaim()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.GetClaimAsync(created.Id, OtherUserId, Role.Underwriter);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
    }

    [Fact]
    public async Task GetClaim_AsAdmin_CanAccessOtherUserClaim()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.GetClaimAsync(created.Id, OtherUserId, Role.Admin);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
    }

    [Fact]
    public async Task GetClaim_NonExistent_ReturnsNull()
    {
        var result = await _service.GetClaimAsync(Guid.NewGuid(), UserId, Role.Policyholder);
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

    [Fact]
    public async Task GetAllClaims_ReturnsAllClaims()
    {
        await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.CreateClaimAsync(OtherUserId, MakeCreateDto());

        var allClaims = await _service.GetAllClaimsAsync();
        Assert.True(allClaims.Count >= 2);
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

    // ── DELETE & WITHDRAW ──

    [Fact]
    public async Task DeleteClaim_OwnerDraftClaim_HardDeletesClaim()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.DeleteClaimAsync(created.Id, UserId, Role.Policyholder);

        Assert.True(result);

        // Verify the claim is hard-deleted
        var claim = await _service.GetClaimAsync(created.Id, UserId, Role.Policyholder);
        Assert.Null(claim);
    }

    [Fact]
    public async Task DeleteClaim_SubmittedClaim_Throws()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteClaimAsync(created.Id, UserId, Role.Policyholder));
    }

    [Fact]
    public async Task DeleteClaim_AsNonOwner_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteClaimAsync(created.Id, OtherUserId, Role.Policyholder));
    }

    [Fact]
    public async Task DeleteClaim_AsClaimsAdjuster_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteClaimAsync(created.Id, UserId, Role.ClaimsAdjuster));
    }

    [Fact]
    public async Task DeleteClaim_AsUnderwriter_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteClaimAsync(created.Id, UserId, Role.Underwriter));
    }

    [Fact]
    public async Task DeleteClaim_AsAdmin_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteClaimAsync(created.Id, UserId, Role.Admin));
    }

    [Fact]
    public async Task DeleteClaim_FinalizedClaim_Throws()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var domainClaim = await _claimRepository.GetByIdAsync(created.Id);
        domainClaim!.Status = ClaimStatus.Closed;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteClaimAsync(created.Id, UserId, Role.Policyholder));
    }

    [Fact]
    public async Task WithdrawClaim_SubmittedClaim_SetsWithdrawn()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);

        var result = await _service.WithdrawClaimAsync(created.Id, UserId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.Equal("Withdrawn", result!.Status);
    }

    [Fact]
    public async Task WithdrawClaim_UnderReviewClaim_SetsWithdrawn()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);
        var domainClaim = await _claimRepository.GetByIdAsync(created.Id);
        domainClaim!.Status = ClaimStatus.UnderReview;

        var result = await _service.WithdrawClaimAsync(created.Id, UserId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.Equal("Withdrawn", result!.Status);
    }

    [Theory]
    [InlineData(Role.ClaimsAdjuster)]
    [InlineData(Role.Underwriter)]
    [InlineData(Role.Admin)]
    public async Task WithdrawClaim_AsStaff_ThrowsUnauthorized(Role staffRole)
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.WithdrawClaimAsync(created.Id, UserId, staffRole));
    }

    [Fact]
    public async Task WithdrawClaim_AsNonOwner_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.WithdrawClaimAsync(created.Id, OtherUserId, Role.Policyholder));
    }

    [Fact]
    public async Task WithdrawClaim_FinalizedClaim_Throws()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await _service.SubmitClaimAsync(created.Id, UserId);
        var domainClaim = await _claimRepository.GetByIdAsync(created.Id);
        domainClaim!.Status = ClaimStatus.Approved;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.WithdrawClaimAsync(created.Id, UserId, Role.Policyholder));
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

    // ── DOCUMENTS ──

    [Fact]
    public async Task AddDocument_AsOwner_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var dto = new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream);

        var doc = await _service.AddDocumentAsync(created.Id, UserId, Role.Policyholder, dto);

        Assert.NotNull(doc);
        Assert.Equal("Police Report", doc.DocumentType);
        Assert.Equal("report.pdf", doc.FileName);
    }

    [Fact]
    public async Task AddDocument_AsOtherPolicyholder_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var dto = new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.AddDocumentAsync(created.Id, OtherUserId, Role.Policyholder, dto));
    }

    [Fact]
    public async Task AddDocument_AsClaimsAdjuster_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var dto = new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream);

        var doc = await _service.AddDocumentAsync(created.Id, OtherUserId, Role.ClaimsAdjuster, dto);

        Assert.NotNull(doc);
        Assert.Equal("Police Report", doc.DocumentType);
    }

    [Fact]
    public async Task GetDocuments_AsOwner_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var docs = await _service.GetDocumentsAsync(created.Id, UserId, Role.Policyholder);
        Assert.NotNull(docs);
    }

    [Fact]
    public async Task GetDocuments_AsOtherPolicyholder_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetDocumentsAsync(created.Id, OtherUserId, Role.Policyholder));
    }

    [Fact]
    public async Task GetDocuments_AsClaimsAdjuster_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var docs = await _service.GetDocumentsAsync(created.Id, OtherUserId, Role.ClaimsAdjuster);
        Assert.NotNull(docs);
    }

    // ── DOCUMENT DELETE ──

    [Fact]
    public async Task DeleteDocument_AsOwner_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var doc = await _service.AddDocumentAsync(created.Id, UserId, Role.Policyholder,
            new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream));

        var result = await _service.DeleteDocumentAsync(created.Id, doc.Id, UserId, Role.Policyholder);
        Assert.True(result);

        var docs = await _service.GetDocumentsAsync(created.Id, UserId, Role.Policyholder);
        Assert.DoesNotContain(docs, d => d.Id == doc.Id);
    }

    [Theory]
    [InlineData(Role.ClaimsAdjuster)]
    [InlineData(Role.Underwriter)]
    [InlineData(Role.Admin)]
    public async Task DeleteDocument_AsStaff_Succeeds(Role staffRole)
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var doc = await _service.AddDocumentAsync(created.Id, UserId, Role.Policyholder,
            new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream));

        var result = await _service.DeleteDocumentAsync(created.Id, doc.Id, OtherUserId, staffRole);
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteDocument_AsNonOwner_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var doc = await _service.AddDocumentAsync(created.Id, UserId, Role.Policyholder,
            new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteDocumentAsync(created.Id, doc.Id, OtherUserId, Role.Policyholder));
    }

    [Fact]
    public async Task DeleteDocument_WrongClaim_ThrowsKeyNotFoundException()
    {
        var created1 = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var created2 = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var doc = await _service.AddDocumentAsync(created1.Id, UserId, Role.Policyholder,
            new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.DeleteDocumentAsync(created2.Id, doc.Id, UserId, Role.Policyholder));
    }

    [Fact]
    public async Task DeleteDocument_FinalizedClaim_ThrowsInvalidOperationException()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var doc = await _service.AddDocumentAsync(created.Id, UserId, Role.Policyholder,
            new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream));

        var domainClaim = await _claimRepository.GetByIdAsync(created.Id);
        domainClaim!.Status = ClaimStatus.Approved;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteDocumentAsync(created.Id, doc.Id, UserId, Role.Policyholder));
    }

    [Fact]
    public async Task DeleteDocument_InvokesStorageDelete()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var doc = await _service.AddDocumentAsync(created.Id, UserId, Role.Policyholder,
            new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream));

        var initialCount = _storageService.DeleteCallCount;
        await _service.DeleteDocumentAsync(created.Id, doc.Id, UserId, Role.Policyholder);

        Assert.True(_storageService.DeleteCallCount > initialCount);
    }

    [Fact]
    public async Task DeleteDocument_WhenStorageFails_ThrowsAndDoesNotDeleteFromDb()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var doc = await _service.AddDocumentAsync(created.Id, UserId, Role.Policyholder,
            new UploadDocumentDto("Police Report", "report.pdf", "application/pdf", 3, stream));

        _storageService.ShouldFail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteDocumentAsync(created.Id, doc.Id, UserId, Role.Policyholder));

        _storageService.ShouldFail = false;

        // Verify document is still present in repository
        var docs = await _service.GetDocumentsAsync(created.Id, UserId, Role.Policyholder);
        Assert.Contains(docs, d => d.Id == doc.Id);
    }

    // ── COVERAGE VALIDATION ──

    [Fact]
    public async Task ValidateCoverage_ReturnsResult()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.ValidateCoverageAsync(created.Id, UserId, Role.Policyholder);

        Assert.True(result.IsValid);
        Assert.True(result.IsCovered);
    }

    [Fact]
    public async Task ValidateCoverage_AsOtherPolicyholder_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateCoverageAsync(created.Id, OtherUserId, Role.Policyholder));
    }

    [Fact]
    public async Task ValidateCoverage_AsClaimsAdjuster_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.ValidateCoverageAsync(created.Id, OtherUserId, Role.ClaimsAdjuster);

        Assert.True(result.IsValid);
        Assert.True(result.IsCovered);
    }

    // ── DOCUMENT VERIFICATION ──

    [Fact]
    public async Task VerifyDocuments_ReturnsAgentResult_WithAiMetadata()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.VerifyDocumentsAsync(created.Id, UserId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.True(result.Complete);
        Assert.True(result.AiUsed);
        Assert.Equal("google", result.AiProvider);
        Assert.Equal("gemini-2.5-flash", result.AiModel);
        Assert.NotNull(result.ReasoningSummary);
        Assert.False(result.FallbackUsed);
    }

    [Fact]
    public async Task VerifyDocuments_AsOtherPolicyholder_ThrowsUnauthorized()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.VerifyDocumentsAsync(created.Id, OtherUserId, Role.Policyholder));
    }

    [Fact]
    public async Task VerifyDocuments_AsClaimsAdjuster_Succeeds()
    {
        var created = await _service.CreateClaimAsync(UserId, MakeCreateDto());
        var result = await _service.VerifyDocumentsAsync(created.Id, OtherUserId, Role.ClaimsAdjuster);

        Assert.NotNull(result);
        Assert.True(result.Complete);
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

    public Task DeleteDocumentAsync(ClaimDocument document)
    {
        var claim = _claims.FirstOrDefault(c => c.Id == document.ClaimId);
        claim?.Documents?.Remove(document);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id) =>
        Task.FromResult(_claims.Any(c => c.Id == id));

    public Task<string> GenerateClaimNumberAsync() =>
        Task.FromResult($"CLM-TEST-{_claims.Count + 1:D4}");
}

internal class FakeDocumentStorageService : IDocumentStorageService
{
    public bool ShouldFail { get; set; } = false;
    public bool ShouldThrow { get; set; } = false;
    public int DeleteCallCount { get; private set; } = 0;

    public Task<string> UploadAsync(string fileName, string contentType, Stream fileStream) =>
        Task.FromResult($"/uploads/fake_{fileName}");

    public Task<bool> DeleteAsync(string fileUrl)
    {
        DeleteCallCount++;
        if (ShouldThrow)
            throw new System.IO.IOException("Simulated storage failure");
        if (ShouldFail)
            return Task.FromResult(false);
        return Task.FromResult(true);
    }
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
            Complete: true,
            MissingItems: new List<string>(),
            Inconsistencies: new List<DocumentInconsistencyDto>(),
            Warnings: new List<string>(),
            AiUsed: true,
            AiProvider: "google",
            AiModel: "gemini-2.5-flash",
            ReasoningSummary: "All documents verified successfully with AI reasoning.",
            FallbackUsed: false
        ));
}
