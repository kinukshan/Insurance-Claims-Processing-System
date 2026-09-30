using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceClaims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceClassAndSeedLifePolicyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InsuranceClass",
                table: "PolicyTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Seed Life Insurance only if absent (non-destructive, idempotent)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM ""PolicyTypes""
                        WHERE ""Id"" = '22222222-2222-4222-8222-222222222224'
                           OR LOWER(TRIM(""Name"")) = 'life insurance'
                    ) THEN
                        INSERT INTO ""PolicyTypes"" (
                            ""Id"", ""Name"", ""Description"", ""BasePremiumRate"",
                            ""DefaultCoverageLimit"", ""DefaultDeductible"", ""RiskMultiplier"",
                            ""IsActive"", ""InsuranceClass"", ""CreatedAt"", ""UpdatedAt""
                        )
                        VALUES (
                            '22222222-2222-4222-8222-222222222224',
                            'Life Insurance',
                            'Long-term life insurance policy covering death benefits',
                            25.00,
                            2000000.00,
                            0.00,
                            1.0,
                            true,
                            1,
                            NOW(),
                            NOW()
                        );
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only remove Life Insurance if no policies reference it (prevent FK constraint failure)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM ""Policies""
                        WHERE ""PolicyTypeId"" = '22222222-2222-4222-8222-222222222224'
                    ) THEN
                        DELETE FROM ""PolicyTypes""
                        WHERE ""Id"" = '22222222-2222-4222-8222-222222222224';
                    END IF;
                END $$;
            ");

            migrationBuilder.DropColumn(
                name: "InsuranceClass",
                table: "PolicyTypes");
        }
    }
}
