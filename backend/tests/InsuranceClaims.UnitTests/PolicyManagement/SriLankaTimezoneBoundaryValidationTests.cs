using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Validators;
using InsuranceClaims.Application.Common;
using InsuranceClaims.Application.Common.Interfaces;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.Validators;
using InsuranceClaims.Domain.ClaimsManagement;
using Xunit;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Deterministic tests for the Sri Lanka timezone boundary (Asia/Colombo UTC+05:30).
/// Verifies that business-calendar "today" rules correctly evaluate date-only fields
/// based on the Sri Lanka business calendar rather than raw UTC date.
/// </summary>
public class SriLankaTimezoneBoundaryValidationTests
{
    // Sri Lanka local time: 2026-10-05 01:00:00 (UTC+05:30)
    // Equivalent UTC instant: 2026-10-04 19:30:00Z
    private static readonly DateTime BoundaryUtcInstant = new(2026, 10, 4, 19, 30, 0, DateTimeKind.Utc);

    private static IBusinessCalendar CreateControlledCalendar() =>
        new BusinessCalendar(() => BoundaryUtcInstant);

    [Fact]
    public void Requirement1_UtcInstant_ConvertsTo_AsiaColombo_BusinessDate()
    {
        // 1. UTC instant: 2026-10-04T19:30:00Z
        // In Asia/Colombo (UTC+05:30), the time is 2026-10-05 01:00:00
        // Asia/Colombo business date must be: 2026-10-05
        var calendar = CreateControlledCalendar();

        var businessDate = calendar.ToBusinessDate(BoundaryUtcInstant);
        Assert.Equal(new DateTime(2026, 10, 5), businessDate);
        Assert.Equal(new DateTime(2026, 10, 5), calendar.Today);
    }

    [Fact]
    public void Requirement2_PolicyStartDate_Equals_BusinessCalendarToday_Accepted()
    {
        // 2. Policy StartDate = 2026-10-05 -> accepted as today.
        // In UTC this instant is 2026-10-04, but in Sri Lanka it is legitimately 2026-10-05.
        // It must NOT be rejected.
        var calendar = CreateControlledCalendar();
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 100000m,
            Deductible = 5000m,
            StartDate = new DateTime(2026, 10, 5),
            ExpiryDate = new DateTime(2027, 10, 5)
        };

        var errors = PolicyValidator.ValidateCreate(dto, calendar);

        Assert.DoesNotContain(errors, e => e.Contains("Start date cannot be before today"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Requirement3_PolicyStartDate_Before_BusinessCalendarToday_Rejected()
    {
        // 3. Policy StartDate = 2026-10-04 -> rejected as past.
        // In UTC the date is 2026-10-04, but in Sri Lanka business calendar today is 2026-10-05.
        // Therefore, 2026-10-04 is yesterday and must be rejected.
        var calendar = CreateControlledCalendar();
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 100000m,
            Deductible = 5000m,
            StartDate = new DateTime(2026, 10, 4),
            ExpiryDate = new DateTime(2027, 10, 4)
        };

        var errors = PolicyValidator.ValidateCreate(dto, calendar);

        Assert.Contains(errors, e => e.Contains("Start date cannot be before today"));
    }

    [Fact]
    public void Requirement4_ClaimIncidentDate_Equals_BusinessCalendarToday_Accepted()
    {
        // 4. Claim IncidentDate = 2026-10-05 -> accepted as today.
        // Under raw UTC (2026-10-04), 2026-10-05 would have incorrectly been rejected as a future date.
        // With business calendar today (2026-10-05), it is accepted.
        var calendar = CreateControlledCalendar();
        var dto = new CreateClaimDto(
            PolicyId: Guid.NewGuid(),
            ClaimType: ClaimType.Auto,
            IncidentDate: new DateTime(2026, 10, 5),
            IncidentLocation: "Colombo, Sri Lanka",
            Description: "Traffic collision on Galle Road",
            ClaimedAmount: 50000m
        );

        var errors = CreateClaimValidator.Validate(dto, calendar);

        Assert.DoesNotContain(errors, e => e.Contains("IncidentDate cannot be in the future"));
        Assert.Empty(errors);
    }

    [Fact]
    public void Requirement5_ClaimIncidentDate_After_BusinessCalendarToday_Rejected()
    {
        // 5. Claim IncidentDate = 2026-10-06 -> rejected as future.
        var calendar = CreateControlledCalendar();
        var dto = new CreateClaimDto(
            PolicyId: Guid.NewGuid(),
            ClaimType: ClaimType.Auto,
            IncidentDate: new DateTime(2026, 10, 6),
            IncidentLocation: "Colombo, Sri Lanka",
            Description: "Future claim attempt",
            ClaimedAmount: 50000m
        );

        var errors = CreateClaimValidator.Validate(dto, calendar);

        Assert.Contains(errors, e => e.Contains("IncidentDate cannot be in the future"));
    }

    [Fact]
    public void Requirement6_ExistingExpiryDateValidations_RemainCorrect()
    {
        var calendar = CreateControlledCalendar();

        // 6a. ExpiryDate < StartDate -> rejected
        var dtoPastExpiry = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 100000m,
            Deductible = 5000m,
            StartDate = new DateTime(2026, 10, 5),
            ExpiryDate = new DateTime(2026, 10, 4)
        };
        var errorsPast = PolicyValidator.ValidateCreate(dtoPastExpiry, calendar);
        Assert.Contains(errorsPast, e => e.Contains("Expiry date must be later than the start date"));

        // 6b. ExpiryDate == StartDate -> rejected
        var dtoSameDay = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 100000m,
            Deductible = 5000m,
            StartDate = new DateTime(2026, 10, 5),
            ExpiryDate = new DateTime(2026, 10, 5)
        };
        var errorsSame = PolicyValidator.ValidateCreate(dtoSameDay, calendar);
        Assert.Contains(errorsSame, e => e.Contains("Expiry date must be later than the start date"));

        // 6c. ExpiryDate > StartDate -> valid
        var dtoValid = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 100000m,
            Deductible = 5000m,
            StartDate = new DateTime(2026, 10, 5),
            ExpiryDate = new DateTime(2026, 10, 6)
        };
        var errorsValid = PolicyValidator.ValidateCreate(dtoValid, calendar);
        Assert.Empty(errorsValid);
    }
}
