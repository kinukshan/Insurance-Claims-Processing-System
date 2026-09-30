using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceClaims.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayPalFieldsAndWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderBatchId",
                table: "PaymentTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderItemId",
                table: "PaymentTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatusRaw",
                table: "PaymentTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "PaymentTransactions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderBatchId",
                table: "PaymentTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderItemId",
                table: "PaymentTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentWebhookEvents_PaymentTransactions_PaymentTransaction~",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ProviderBatchId",
                table: "PaymentTransactions",
                column: "ProviderBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ProviderItemId",
                table: "PaymentTransactions",
                column: "ProviderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SenderBatchId",
                table: "PaymentTransactions",
                column: "SenderBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SenderItemId",
                table: "PaymentTransactions",
                column: "SenderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_EventType",
                table: "PaymentWebhookEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_PaymentTransactionId",
                table: "PaymentWebhookEvents",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_ProviderEventId",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_ReceivedAt",
                table: "PaymentWebhookEvents",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_ProviderBatchId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_ProviderItemId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_SenderBatchId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_SenderItemId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderBatchId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderItemId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderStatusRaw",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SenderBatchId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SenderItemId",
                table: "PaymentTransactions");
        }
    }
}
