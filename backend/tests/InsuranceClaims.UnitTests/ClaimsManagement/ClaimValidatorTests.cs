using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Validators;
using InsuranceClaims.Domain.ClaimsManagement;

namespace InsuranceClaims.UnitTests.ClaimsManagement;

/// <summary>
/// Unit tests for claim validators — CreateClaimValidator,
/// UpdateClaimValidator, and DocumentChecklistValidator.
/// </summary>
public class ClaimValidatorTests
{
    // ── CreateClaimValidator ──

    [Fact]
    public void CreateValidator_ValidData_NoErrors()
    {
        var dto = new CreateClaimDto(
            Guid.NewGuid(), ClaimType.Auto,
            DateTime.UtcNow.AddDays(-5), "Test Location",
            "Valid description", 1000m);

        var errors = CreateClaimValidator.Validate(dto);
        Assert.Empty(errors);
    }

    [Fact]
    public void CreateValidator_EmptyPolicyId_ReturnsError()
    {
        var dto = new CreateClaimDto(
            Guid.Empty, ClaimType.Auto,
            DateTime.UtcNow.AddDays(-5), "Test",
            "Description", 1000m);

        var errors = CreateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("PolicyId"));
    }

    [Fact]
    public void CreateValidator_EmptyDescription_ReturnsError()
    {
        var dto = new CreateClaimDto(
            Guid.NewGuid(), ClaimType.Auto,
            DateTime.UtcNow.AddDays(-5), "Location",
            "", 1000m);

        var errors = CreateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("Description"));
    }

    [Fact]
    public void CreateValidator_ZeroAmount_ReturnsError()
    {
        var dto = new CreateClaimDto(
            Guid.NewGuid(), ClaimType.Auto,
            DateTime.UtcNow.AddDays(-5), "Location",
            "Description", 0m);

        var errors = CreateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("ClaimedAmount"));
    }

    [Fact]
    public void CreateValidator_FutureDate_ReturnsError()
    {
        var dto = new CreateClaimDto(
            Guid.NewGuid(), ClaimType.Auto,
            DateTime.UtcNow.AddDays(10), "Location",
            "Description", 1000m);

        var errors = CreateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("IncidentDate"));
    }

    [Fact]
    public void CreateValidator_AncientDate_ReturnsError()
    {
        var dto = new CreateClaimDto(
            Guid.NewGuid(), ClaimType.Auto,
            DateTime.UtcNow.AddYears(-15), "Location",
            "Description", 1000m);

        var errors = CreateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("10 years"));
    }

    [Fact]
    public void CreateValidator_EmptyLocation_ReturnsError()
    {
        var dto = new CreateClaimDto(
            Guid.NewGuid(), ClaimType.Auto,
            DateTime.UtcNow.AddDays(-5), "  ",
            "Description", 1000m);

        var errors = CreateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("IncidentLocation"));
    }

    // ── UpdateClaimValidator ──

    [Fact]
    public void UpdateValidator_AllNull_NoErrors()
    {
        var dto = new UpdateClaimDto(null, null, null, null);
        var errors = UpdateClaimValidator.Validate(dto);
        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateValidator_NegativeAmount_ReturnsError()
    {
        var dto = new UpdateClaimDto(null, null, -500m, null);
        var errors = UpdateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("ClaimedAmount"));
    }

    [Fact]
    public void UpdateValidator_FutureDate_ReturnsError()
    {
        var dto = new UpdateClaimDto(null, null, null, DateTime.UtcNow.AddDays(5));
        var errors = UpdateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("IncidentDate"));
    }

    [Fact]
    public void UpdateValidator_EmptyDescription_ReturnsError()
    {
        var dto = new UpdateClaimDto("  ", null, null, null);
        var errors = UpdateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("Description"));
    }

    [Fact]
    public void UpdateValidator_EmptyLocation_ReturnsError()
    {
        var dto = new UpdateClaimDto(null, "", null, null);
        var errors = UpdateClaimValidator.Validate(dto);
        Assert.Contains(errors, e => e.Contains("IncidentLocation"));
    }

    // ── DocumentChecklistValidator ──

    [Fact]
    public void ChecklistValidator_AutoWithAllDocs_NothingMissing()
    {
        var submitted = new List<string>
        {
            "Police Report", "Photos of Damage", "Repair Estimate", "Driver License"
        };

        var missing = DocumentChecklistValidator.GetMissingDocuments("Auto", submitted);
        Assert.Empty(missing);
    }

    [Fact]
    public void ChecklistValidator_AutoMissingDocs_ReturnsMissing()
    {
        var submitted = new List<string> { "Police Report" };
        var missing = DocumentChecklistValidator.GetMissingDocuments("Auto", submitted);

        Assert.Contains("Photos of Damage", missing);
        Assert.Contains("Repair Estimate", missing);
        Assert.Contains("Driver License", missing);
        Assert.DoesNotContain("Police Report", missing);
    }

    [Fact]
    public void ChecklistValidator_HealthType_RequiresMedicalDocs()
    {
        var required = DocumentChecklistValidator.GetRequiredDocuments("Health");
        Assert.Contains("Medical Report", required);
        Assert.Contains("Hospital Bills", required);
        Assert.Contains("Prescription", required);
        Assert.Contains("Doctor Referral", required);
    }

    [Fact]
    public void ChecklistValidator_UnknownType_RequiresSupporting()
    {
        var required = DocumentChecklistValidator.GetRequiredDocuments("Unknown");
        Assert.Single(required);
        Assert.Equal("Supporting Document", required[0]);
    }

    [Fact]
    public void ChecklistValidator_CaseInsensitive_Match()
    {
        var submitted = new List<string>
        {
            "police report", "photos of damage", "repair estimate", "driver license"
        };

        var missing = DocumentChecklistValidator.GetMissingDocuments("Auto", submitted);
        Assert.Empty(missing);
    }
}
