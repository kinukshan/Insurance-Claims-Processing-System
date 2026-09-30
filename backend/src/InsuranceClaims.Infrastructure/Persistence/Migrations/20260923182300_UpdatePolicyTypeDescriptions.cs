using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceClaims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePolicyTypeDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Coverage for insured motor vehicles and related losses.',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222221'
                   OR LOWER(TRIM(""Name"")) = 'motor insurance';

                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Coverage for eligible medical and healthcare expenses.',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222222'
                   OR LOWER(TRIM(""Name"")) = 'health insurance';

                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Coverage for residential property and insured property damage.',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222223'
                   OR LOWER(TRIM(""Name"")) = 'home insurance';

                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Long-term life insurance covering eligible death benefits.',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222224'
                   OR LOWER(TRIM(""Name"")) = 'life insurance';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Test motor vehicle insurance policy type',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222221'
                   OR LOWER(TRIM(""Name"")) = 'motor insurance';

                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Test health insurance policy type',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222222'
                   OR LOWER(TRIM(""Name"")) = 'health insurance';

                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Test residential property insurance policy type',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222223'
                   OR LOWER(TRIM(""Name"")) = 'home insurance';

                UPDATE ""PolicyTypes""
                SET ""Description"" = 'Long-term life insurance policy covering death benefits',
                    ""UpdatedAt"" = NOW()
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222224'
                   OR LOWER(TRIM(""Name"")) = 'life insurance';
            ");
        }
    }
}
