using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceClaims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeductiblePercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDeductiblePercentage",
                table: "PolicyTypes",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeductiblePercentage",
                table: "Policies",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeductiblePercentage",
                table: "Payouts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""PolicyTypes""
                SET ""DefaultDeductiblePercentage"" = 5.00
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222221'
                   OR LOWER(TRIM(""Name"")) = 'motor insurance';

                UPDATE ""PolicyTypes""
                SET ""DefaultDeductiblePercentage"" = 10.00
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222222'
                   OR LOWER(TRIM(""Name"")) = 'health insurance';

                UPDATE ""PolicyTypes""
                SET ""DefaultDeductiblePercentage"" = 10.00
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222223'
                   OR LOWER(TRIM(""Name"")) = 'home insurance';

                UPDATE ""PolicyTypes""
                SET ""DefaultDeductiblePercentage"" = 0.00
                WHERE ""Id"" = '22222222-2222-4222-8222-222222222224'
                   OR LOWER(TRIM(""Name"")) = 'life insurance';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultDeductiblePercentage",
                table: "PolicyTypes");

            migrationBuilder.DropColumn(
                name: "DeductiblePercentage",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "DeductiblePercentage",
                table: "Payouts");
        }
    }
}
