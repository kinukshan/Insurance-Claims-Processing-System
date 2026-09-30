using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.Users;

namespace InsuranceClaims.UnitTests.ClaimsManagement;

/// <summary>
/// Unit tests for deterministic document requirements API and service logic:
/// - Claim type-specific requirement lists
/// - Ownership isolation (own claim vs another user's claim vs staff)
/// - Missing claim handling
/// - Alias normalization (Beneficiary ID -> Beneficiary / Nominee Identification)
/// - Deduplication of aliases
/// - Progress counts (required, uploaded, missing, complete)
/// </summary>
public class ClaimDocumentRequirementsTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherPolicyholderId = Guid.NewGuid();
    private static readonly Guid AdjusterId = Guid.NewGuid();

    private readonly FakeClaimRepository _claimRepository = new();
    private readonly FakeDocumentStorageService _storageService = new();
    private readonly FakePolicyValidationService _policyValidation = new();
    private readonly FakeDocumentVerificationClient _verificationClient = new();
    private readonly ClaimService _service;

    public ClaimDocumentRequirementsTests()
    {
        _service = new ClaimService(_claimRepository, _storageService, _policyValidation, _verificationClient);
    }

    // ── 1. Ownership & Authorization ──

    [Fact]
    public async Task GetDocumentRequirements_MissingClaim_ReturnsNull_404()
    {
        var result = await _service.GetDocumentRequirementsAsync(Guid.NewGuid(), OwnerId, Role.Policyholder);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDocumentRequirements_AnotherPolicyholderClaim_ThrowsUnauthorized_403()
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-AUTH-001",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted
        };
        await _claimRepository.AddAsync(claim);

        // OtherPolicyholder cannot view requirements of Owner's claim
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetDocumentRequirementsAsync(claim.Id, OtherPolicyholderId, Role.Policyholder));
    }

    [Fact]
    public async Task GetDocumentRequirements_StaffRole_CanViewAnyClaim()
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-AUTH-002",
            ClaimType = ClaimType.Motor,
            Status = ClaimStatus.Submitted
        };
        await _claimRepository.AddAsync(claim);

        // ClaimsAdjuster can view requirements
        var result = await _service.GetDocumentRequirementsAsync(claim.Id, AdjusterId, Role.ClaimsAdjuster);
        Assert.NotNull(result);
        Assert.Equal(claim.Id, result.ClaimId);
    }

    // ── 2. Requirement Lists by Claim Type ──

    [Theory]
    [InlineData(ClaimType.Motor, new[] { "Police Report", "Photos of Damage", "Repair Estimate", "Driver License" })]
    [InlineData(ClaimType.Auto, new[] { "Police Report", "Photos of Damage", "Repair Estimate", "Driver License" })]
    [InlineData(ClaimType.Property, new[] { "Photos of Damage", "Repair Estimate", "Property Valuation" })]
    [InlineData(ClaimType.Home, new[] { "Photos of Damage", "Repair Estimate", "Property Deed" })]
    [InlineData(ClaimType.Health, new[] { "Medical Report", "Hospital Bills", "Prescription", "Doctor Referral" })]
    [InlineData(ClaimType.Life, new[] { "Death Certificate", "Policy Document", "Beneficiary / Nominee Identification", "Claim Form" })]
    public async Task GetDocumentRequirements_ReturnsAuthoritativeList_ForClaimType(ClaimType claimType, string[] expectedTypes)
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = OwnerId,
            ClaimNumber = $"CLM-{claimType}-001",
            ClaimType = claimType,
            Status = ClaimStatus.Submitted
        };
        await _claimRepository.AddAsync(claim);

        var result = await _service.GetDocumentRequirementsAsync(claim.Id, OwnerId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.Equal(expectedTypes.Length, result.RequiredCount);
        Assert.Equal(0, result.UploadedRequiredCount);
        Assert.Equal(expectedTypes.Length, result.MissingCount);
        Assert.False(result.Complete);

        var returnedTypes = result.RequiredDocuments.Select(r => r.Type).ToList();
        Assert.Equal(expectedTypes, returnedTypes);
        Assert.All(result.RequiredDocuments, r => Assert.False(r.Uploaded));
    }

    // ── 3. Uploaded / Missing Status Matching ──

    [Fact]
    public async Task GetDocumentRequirements_PropertyClaim_PartiallyUploaded_ComputesCountsCorrectly()
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-PROP-001",
            ClaimType = ClaimType.Property,
            Status = ClaimStatus.Submitted
        };
        // Property requires: Photos of Damage, Repair Estimate, Property Valuation
        // Upload 2 of 3:
        claim.Documents.Add(new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FileName = "damage1.jpg",
            DocumentType = "Photos of Damage"
        });
        claim.Documents.Add(new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FileName = "estimate.pdf",
            DocumentType = "Repair Estimate"
        });
        await _claimRepository.AddAsync(claim);

        var result = await _service.GetDocumentRequirementsAsync(claim.Id, OwnerId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.Equal(3, result.RequiredCount);
        Assert.Equal(2, result.UploadedRequiredCount);
        Assert.Equal(1, result.MissingCount);
        Assert.False(result.Complete);

        var photos = result.RequiredDocuments.First(r => r.Type == "Photos of Damage");
        var estimate = result.RequiredDocuments.First(r => r.Type == "Repair Estimate");
        var valuation = result.RequiredDocuments.First(r => r.Type == "Property Valuation");

        Assert.True(photos.Uploaded);
        Assert.True(estimate.Uploaded);
        Assert.False(valuation.Uploaded);
    }

    [Fact]
    public async Task GetDocumentRequirements_AllUploaded_CompleteIsTrue()
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-PROP-002",
            ClaimType = ClaimType.Property,
            Status = ClaimStatus.Submitted
        };
        claim.Documents.Add(new ClaimDocument { Id = Guid.NewGuid(), ClaimId = claim.Id, FileName = "f1.jpg", DocumentType = "Photos of Damage" });
        claim.Documents.Add(new ClaimDocument { Id = Guid.NewGuid(), ClaimId = claim.Id, FileName = "f2.pdf", DocumentType = "Repair Estimate" });
        claim.Documents.Add(new ClaimDocument { Id = Guid.NewGuid(), ClaimId = claim.Id, FileName = "f3.pdf", DocumentType = "Property Valuation" });
        await _claimRepository.AddAsync(claim);

        var result = await _service.GetDocumentRequirementsAsync(claim.Id, OwnerId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.Equal(3, result.RequiredCount);
        Assert.Equal(3, result.UploadedRequiredCount);
        Assert.Equal(0, result.MissingCount);
        Assert.True(result.Complete);
    }

    // ── 4. Life Alias Normalization & Deduplication ──

    [Fact]
    public async Task GetDocumentRequirements_LifeClaim_BeneficiaryIdAlias_SatisfiesRequirement()
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-LIFE-001",
            ClaimType = ClaimType.Life,
            Status = ClaimStatus.Submitted
        };
        // Upload historical "Beneficiary ID" alias
        claim.Documents.Add(new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FileName = "nominee_nic.pdf",
            DocumentType = "Beneficiary ID"
        });
        await _claimRepository.AddAsync(claim);

        var result = await _service.GetDocumentRequirementsAsync(claim.Id, OwnerId, Role.Policyholder);

        Assert.NotNull(result);
        Assert.Equal(4, result.RequiredCount);
        Assert.Equal(1, result.UploadedRequiredCount);

        var beneficiaryReq = result.RequiredDocuments.First(r => r.Type == "Beneficiary / Nominee Identification");
        Assert.True(beneficiaryReq.Uploaded);
    }

    [Fact]
    public async Task GetDocumentRequirements_LifeClaim_DuplicateAliases_CountAsSingleCompletedRequirement()
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = OwnerId,
            ClaimNumber = "CLM-LIFE-002",
            ClaimType = ClaimType.Life,
            Status = ClaimStatus.Submitted
        };
        // Upload both "Beneficiary ID" and "Beneficiary / Nominee Identification"
        claim.Documents.Add(new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FileName = "file1.pdf",
            DocumentType = "Beneficiary ID"
        });
        claim.Documents.Add(new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            FileName = "file2.pdf",
            DocumentType = "Beneficiary / Nominee Identification"
        });
        await _claimRepository.AddAsync(claim);

        var result = await _service.GetDocumentRequirementsAsync(claim.Id, OwnerId, Role.Policyholder);

        Assert.NotNull(result);
        // Only 1 requirement should be marked as uploaded, NOT 2!
        Assert.Equal(1, result.UploadedRequiredCount);
        Assert.Equal(3, result.MissingCount);
        Assert.False(result.Complete);

        var beneficiaryReq = result.RequiredDocuments.First(r => r.Type == "Beneficiary / Nominee Identification");
        Assert.True(beneficiaryReq.Uploaded);
    }
}
