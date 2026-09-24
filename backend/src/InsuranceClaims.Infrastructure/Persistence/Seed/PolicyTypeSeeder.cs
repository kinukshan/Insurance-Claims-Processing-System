using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seeder that ensures the 4 canonical policy types exist in the database.
/// SAFE BEHAVIOR:
/// - Existing Motor, Health, Home records are NEVER overwritten (preserves coverage limits, deductibles, rates).
/// - Life Insurance is inserted only if absent.
/// - Duplicate row creation is strictly prevented via deterministic ID and case-insensitive name matching.
/// </summary>
public static class PolicyTypeSeeder
{
    public static async Task EnsurePolicyTypesSeededAsync(
        ApplicationDbContext context,
        ILogger? logger = null)
    {
        var existingTypes = await context.PolicyTypes.ToListAsync();

        var canonicalTypes = new[]
        {
            new PolicyType
            {
                Id = PolicyClaimCompatibility.MotorInsuranceId,
                Name = PolicyClaimCompatibility.MotorInsurance,
                Description = "Coverage for insured motor vehicles and related losses.",
                BasePremiumRate = 15.00m,
                DefaultCoverageLimit = 500000.00m,
                DefaultDeductible = 10000.00m,
                RiskMultiplier = 1.0m,
                InsuranceClass = InsuranceClass.General,
                IsActive = true
            },
            new PolicyType
            {
                Id = PolicyClaimCompatibility.HealthInsuranceId,
                Name = PolicyClaimCompatibility.HealthInsurance,
                Description = "Coverage for eligible medical and healthcare expenses.",
                BasePremiumRate = 20.00m,
                DefaultCoverageLimit = 1000000.00m,
                DefaultDeductible = 5000.00m,
                RiskMultiplier = 1.0m,
                InsuranceClass = InsuranceClass.General,
                IsActive = true
            },
            new PolicyType
            {
                Id = PolicyClaimCompatibility.HomeInsuranceId,
                Name = PolicyClaimCompatibility.HomeInsurance,
                Description = "Coverage for residential property and insured property damage.",
                BasePremiumRate = 10.00m,
                DefaultCoverageLimit = 750000.00m,
                DefaultDeductible = 15000.00m,
                RiskMultiplier = 1.0m,
                InsuranceClass = InsuranceClass.General,
                IsActive = true
            },
            new PolicyType
            {
                Id = PolicyClaimCompatibility.LifeInsuranceId,
                Name = PolicyClaimCompatibility.LifeInsurance,
                Description = "Long-term life insurance covering eligible death benefits.",
                BasePremiumRate = 25.00m,
                DefaultCoverageLimit = 2000000.00m,
                DefaultDeductible = 0.00m, // Project Business Rule: Life Insurance deductible is 0
                RiskMultiplier = 1.0m,
                InsuranceClass = InsuranceClass.LongTerm,
                IsActive = true
            }
        };

        bool changesMade = false;

        foreach (var canonical in canonicalTypes)
        {
            // Match by deterministic ID or case-insensitive canonical name
            var existing = existingTypes.FirstOrDefault(e =>
                e.Id == canonical.Id ||
                string.Equals(e.Name.Trim(), canonical.Name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                // New record: add canonical definition
                canonical.CreatedAt = DateTime.UtcNow;
                canonical.UpdatedAt = DateTime.UtcNow;
                context.PolicyTypes.Add(canonical);
                changesMade = true;
                logger?.LogInformation("Seeded policy type '{Name}' with ID {Id}", canonical.Name, canonical.Id);
            }
            else
            {
                // Existing record: PRESERVE all business values (limits, deductibles, rates)
                // Only update InsuranceClass if it hasn't been set to LongTerm for Life
                if (canonical.Id == PolicyClaimCompatibility.LifeInsuranceId && existing.InsuranceClass != InsuranceClass.LongTerm)
                {
                    existing.InsuranceClass = InsuranceClass.LongTerm;
                    existing.UpdatedAt = DateTime.UtcNow;
                    changesMade = true;
                }
            }
        }

        if (changesMade)
        {
            await context.SaveChangesAsync();
        }
    }
}
