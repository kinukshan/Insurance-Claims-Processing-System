using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Application.Common.Exceptions;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Exceptions;
using InsuranceClaims.Domain.Users;

namespace InsuranceClaims.UnitTests.ClaimsManagement;

/// <summary>
/// Unit tests verifying the strict claim creation order, ownership verification,
/// anti-leakage defense, staff claim attribution, and historical claim compatibility.
/// </summary>
public class ClaimCreationOrderAndOwnershipTests
{
    private static readonly Guid PolicyOwnerId = Guid.NewGuid();
    private static readonly Guid AttackerPolicyholderId = Guid.NewGuid();
    private static readonly Guid StaffUserId = Guid.NewGuid();
    private static readonly Guid TargetPolicyId = Guid.NewGuid();

    private readonly FakeClaimRepository _claimRepository = new();
    private readonly FakeDocumentStorageService _storageService = new();
    private readonly ConfigurablePolicyValidationService _policyValidation = new();
    private readonly FakeDocumentVerificationClient _verificationClient = new();
    private readonly ClaimService _service;

    public ClaimCreationOrderAndOwnershipTests()
    {
        _service = new ClaimService(_claimRepository, _storageService, _policyValidation, _verificationClient);
    }

    [Fact]
    public async Task CreateClaim_PolicyNotFound_ThrowsKeyNotFoundException()
    {
        _policyValidation.PolicyExists = false;

        var dto = new CreateClaimDto(
            TargetPolicyId,
            ClaimType.Motor,
            DateTime.UtcNow.AddDays(-1),
            "Colombo",
            "Accident description",
            50000m);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CreateClaimAsync(PolicyOwnerId, dto));
    }

    [Fact]
    public async Task CreateClaim_NonOwnerPolicyholder_ThrowsForbiddenException_403()
    {
        _policyValidation.PolicyExists = true;
        _policyValidation.PolicyOwnerId = PolicyOwnerId; // Belongs to PolicyOwnerId
        _policyValidation.PolicyTypeName = "Motor Insurance";

        var dto = new CreateClaimDto(
            TargetPolicyId,
            ClaimType.Motor, // Compatible claim type
            DateTime.UtcNow.AddDays(-1),
            "Colombo",
            "Accident description",
            50000m);

        // Attacker is NOT the owner
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.CreateClaimAsync(AttackerPolicyholderId, dto));
    }

    [Fact]
    public async Task CreateClaim_OwnershipCheckOccursBeforeCompatibility_PreventsPolicyTypeLeakage()
    {
        // TARGET policy belongs to PolicyOwnerId and is "Life Insurance"
        _policyValidation.PolicyExists = true;
        _policyValidation.PolicyOwnerId = PolicyOwnerId;
        _policyValidation.PolicyTypeName = "Life Insurance";

        // Attacker crafts a probe with ClaimType.Motor (which is incompatible with Life Insurance)
        var probeDto = new CreateClaimDto(
            TargetPolicyId,
            ClaimType.Motor,
            DateTime.UtcNow.AddDays(-1),
            "Colombo",
            "Probe claim description",
            50000m);

        // MUST throw UnauthorizedAccessException (mapped to 403 Forbidden), NOT PolicyClaimCompatibilityException (400)!
        // If it threw PolicyClaimCompatibilityException, it would reveal that the policy type is incompatible (leaking information)!
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.CreateClaimAsync(AttackerPolicyholderId, probeDto));

        Assert.Contains("permission", ex.Message);
    }

    [Fact]
    public async Task CreateClaim_OwnerWithIncompatibleClaimType_ThrowsPolicyClaimCompatibilityException_400()
    {
        _policyValidation.PolicyExists = true;
        _policyValidation.PolicyOwnerId = PolicyOwnerId;
        _policyValidation.PolicyTypeName = "Motor Insurance";

        // Owner attempts to file Property claim against Motor policy
        var dto = new CreateClaimDto(
            TargetPolicyId,
            ClaimType.Property,
            DateTime.UtcNow.AddDays(-1),
            "Colombo",
            "Damage to property",
            50000m);

        var ex = await Assert.ThrowsAsync<PolicyClaimCompatibilityException>(() =>
            _service.CreateClaimAsync(PolicyOwnerId, dto));

        Assert.Contains("Motor Insurance", ex.Message);
    }

    [Theory]
    [InlineData("Motor Insurance", ClaimType.Motor)]
    [InlineData("Motor Insurance", ClaimType.Auto)] // Historical Auto accepted
    [InlineData("Health Insurance", ClaimType.Health)]
    [InlineData("Home Insurance", ClaimType.Property)]
    [InlineData("Home Insurance", ClaimType.Home)] // Historical Home accepted
    [InlineData("Home / Property Insurance", ClaimType.Property)]
    [InlineData("Life Insurance", ClaimType.Life)]
    public async Task CreateClaim_CompatibleClaim_Succeeds(string policyTypeName, ClaimType claimType)
    {
        _policyValidation.PolicyExists = true;
        _policyValidation.PolicyOwnerId = PolicyOwnerId;
        _policyValidation.PolicyTypeName = policyTypeName;

        var dto = new CreateClaimDto(
            TargetPolicyId,
            claimType,
            DateTime.UtcNow.AddDays(-1),
            "Colombo",
            "Valid incident description",
            25000m);

        var result = await _service.CreateClaimAsync(PolicyOwnerId, dto);

        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(PolicyOwnerId, result.PolicyHolderId);
        Assert.Equal(claimType.ToString(), result.ClaimType);
    }

    [Fact]
    public async Task CreateClaim_ByStaff_AssignsPolicyOwnerId_NeverStaffUserId()
    {
        _policyValidation.PolicyExists = true;
        _policyValidation.PolicyOwnerId = PolicyOwnerId;
        _policyValidation.PolicyTypeName = "Motor Insurance";

        var dto = new CreateClaimDto(
            TargetPolicyId,
            ClaimType.Motor,
            DateTime.UtcNow.AddDays(-1),
            "Colombo",
            "Staff-initiated claim on behalf of customer",
            30000m);

        // Staff (ClaimsAdjuster) initiates claim
        var result = await _service.CreateClaimAsync(StaffUserId, dto, Role.ClaimsAdjuster);

        Assert.NotNull(result);
        // Authoritative: Claim.PolicyHolderId MUST be the policy's real owner, NEVER the staff member's ID
        Assert.Equal(PolicyOwnerId, result.PolicyHolderId);
        Assert.NotEqual(StaffUserId, result.PolicyHolderId);
    }

    // ── Supporting test double ──

    private class ConfigurablePolicyValidationService : IPolicyValidationService
    {
        public bool PolicyExists { get; set; } = true;
        public Guid PolicyOwnerId { get; set; } = Guid.NewGuid();
        public string PolicyTypeName { get; set; } = "Motor Insurance";

        public Task<CoverageValidationResultDto> ValidateCoverageAsync(Guid policyId, string claimType, decimal claimedAmount) =>
            Task.FromResult(new CoverageValidationResultDto(true, true, 100000m, 500m, claimType, new List<string>()));

        public Task<bool> IsPolicyActiveAsync(Guid policyId) =>
            Task.FromResult(true);

        public Task<bool> ValidatePolicyOwnershipAsync(Guid policyId, Guid policyHolderId) =>
            Task.FromResult(PolicyExists && policyHolderId == PolicyOwnerId);

        public Task<Guid?> GetPolicyOwnerIdAsync(Guid policyId) =>
            Task.FromResult<Guid?>(PolicyExists ? PolicyOwnerId : null);

        public Task<PolicyValidationDetailsDto?> GetPolicyDetailsAsync(Guid policyId)
        {
            if (!PolicyExists) return Task.FromResult<PolicyValidationDetailsDto?>(null);

            return Task.FromResult<PolicyValidationDetailsDto?>(new PolicyValidationDetailsDto(
                policyId,
                PolicyOwnerId,
                Guid.NewGuid(),
                PolicyTypeName,
                true
            ));
        }
    }
}
