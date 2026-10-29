using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogPlatformFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatform",
                table: "AuditLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Rows written before this release: the operator's actions move to the platform history, so a cafe's
            // admins stop seeing them. Every other row stays with its cafe (old rows already sit in the first cafe).
            migrationBuilder.Sql("""
                UPDATE "AuditLogs" SET "IsPlatform" = TRUE
                WHERE "Action" IN (
                    'CafeCreated', 'CafeEnabled', 'CafeDisabled', 'CafeAccessSaved', 'CafeSecretSet', 'CafeSecretCleared',
                    'CafeDataExported', 'ModuleLicenseUpdated', 'MaintenanceEnabled', 'MaintenanceDisabled', 'BrandUpdated',
                    'LoyaltyImport', 'LoyaltyLedgerSwitched', 'FiscalSettingsSaved', 'PaymentSettingsSaved',
                    'BillingPlanSaved', 'BillingSubscribed', 'BillingSubscriptionChanged', 'BillingPaymentFailed',
                    'BillingInvoicePaid', 'AppReleaseCreated', 'AppReleaseUpdated', 'AppReleaseDeleted');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatform",
                table: "AuditLogs");
        }
    }
}
