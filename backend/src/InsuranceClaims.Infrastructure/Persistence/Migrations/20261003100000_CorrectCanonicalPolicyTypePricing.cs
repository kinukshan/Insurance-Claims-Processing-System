using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceClaims.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One-time data migration to correct stale canonical PolicyType pricing values
    /// in production and recalculate Draft policy premiums accordingly.
    ///
    /// Root cause: PolicyTypeSeeder intentionally preserves existing rows, so production
    /// PolicyTypes retained old BasePremiumRate values (e.g. Motor=1500 instead of 15).
    ///
    /// This migration:
    /// 1. Corrects the 4 canonical PolicyType rows to canonical rates (15/20/10/25, risk 1.0).
    /// 2. Recalculates Premium for Draft policies only, using the canonical formula.
    /// 3. Does NOT touch Active, Cancelled, Expired, or Lapsed policy premiums.
    /// 4. Does NOT alter claims, payouts, or any other tables.
    /// </summary>
    public partial class CorrectCanonicalPolicyTypePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ================================================================
            // Step 1: Correct the 4 canonical PolicyType pricing values.
            //
            // Uses deterministic IDs as primary match with defensive
            // case-insensitive name fallback.
            // ================================================================

            migrationBuilder.Sql(@"
                -- Motor Insurance: BasePremiumRate = 15.00, RiskMultiplier = 1.00
                UPDATE ""PolicyTypes""
                SET ""BasePremiumRate"" = 15.00,
                    ""RiskMultiplier"" = 1.00,
                    ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222221'
                   OR LOWER(TRIM(""Name"")) = 'motor insurance';

                -- Health Insurance: BasePremiumRate = 20.00, RiskMultiplier = 1.00
                UPDATE ""PolicyTypes""
                SET ""BasePremiumRate"" = 20.00,
                    ""RiskMultiplier"" = 1.00,
                    ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222222'
                   OR LOWER(TRIM(""Name"")) = 'health insurance';

                -- Home Insurance: BasePremiumRate = 10.00, RiskMultiplier = 1.00
                UPDATE ""PolicyTypes""
                SET ""BasePremiumRate"" = 10.00,
                    ""RiskMultiplier"" = 1.00,
                    ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222223'
                   OR LOWER(TRIM(""Name"")) = 'home insurance';

                -- Life Insurance: BasePremiumRate = 25.00, RiskMultiplier = 1.00
                UPDATE ""PolicyTypes""
                SET ""BasePremiumRate"" = 25.00,
                    ""RiskMultiplier"" = 1.00,
                    ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222224'
                   OR LOWER(TRIM(""Name"")) = 'life insurance';
            ");

            // ================================================================
            // Step 2: Recalculate Premium for Draft policies ONLY.
            //
            // SCOPED to the 4 canonical PolicyType deterministic IDs.
            // Unrelated/custom PolicyType Draft policies are NOT touched.
            //
            // Formula:
            //   basePremium = pt.BasePremiumRate * p.CoverageLimit * pt.RiskMultiplier / 1000
            //   deductibleDiscount =
            //       CASE WHEN p.DeductiblePercentage IS NULL AND p.Deductible > 0
            //            THEN ROUND(p.Deductible * 0.05, 2)
            //            ELSE 0 END
            //   premium = ROUND(GREATEST(basePremium - deductibleDiscount, 0), 2)
            //
            // Status is stored as text string 'Draft' in the database
            // (EF .HasConversion<string>(), column type character varying(20)).
            // Active, Cancelled, Expired, Lapsed policies are NOT modified.
            // ================================================================

            migrationBuilder.Sql(@"
                UPDATE ""Policies"" p
                SET ""Premium"" = ROUND(
                    GREATEST(
                        (pt.""BasePremiumRate"" * p.""CoverageLimit"" * pt.""RiskMultiplier"" / 1000)
                        - CASE
                            WHEN p.""DeductiblePercentage"" IS NULL AND p.""Deductible"" > 0
                            THEN ROUND(p.""Deductible"" * 0.05, 2)
                            ELSE 0
                          END,
                        0
                    ), 2),
                    ""UpdatedAt"" = NOW() AT TIME ZONE 'UTC'
                FROM ""PolicyTypes"" pt
                WHERE p.""PolicyTypeId"" = pt.""Id""
                  AND p.""Status"" = 'Draft'
                  AND pt.""Id"" IN (
                      '22222222-2222-4222-8222-222222222221',
                      '22222222-2222-4222-8222-222222222222',
                      '22222222-2222-4222-8222-222222222223',
                      '22222222-2222-4222-8222-222222222224'
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Migration 'CorrectCanonicalPolicyTypePricing' cannot be reversed. "
                + "This migration corrects canonical PolicyType business pricing data "
                + "(BasePremiumRate, RiskMultiplier) and recalculates Draft policy premiums. "
                + "The original per-database pricing values are not captured and cannot be "
                + "safely restored programmatically. To roll back, restore from a database "
                + "snapshot or backup taken before this migration was applied.");
        }
    }
}
