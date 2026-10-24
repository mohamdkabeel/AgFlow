using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncAllChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientContact_Clients_ClientId",
                table: "ClientContact");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItem_Invoices_InvoiceId",
                table: "InvoiceLineItem");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_WorkspaceId_CreatedAt",
                table: "TimeEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InvoiceLineItem",
                table: "InvoiceLineItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClientContact",
                table: "ClientContact");

            migrationBuilder.DropIndex(
                name: "IX_ClientContact_ClientId",
                table: "ClientContact");

            migrationBuilder.RenameTable(
                name: "InvoiceLineItem",
                newName: "InvoiceLineItems");

            migrationBuilder.RenameTable(
                name: "ClientContact",
                newName: "ClientContacts");

            migrationBuilder.RenameIndex(
                name: "IX_InvoiceLineItem_InvoiceId",
                table: "InvoiceLineItems",
                newName: "IX_InvoiceLineItems_InvoiceId");

            migrationBuilder.AddColumn<bool>(
                name: "RequireTimeApproval",
                table: "Workspaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyCost",
                table: "WorkspaceMembers",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "TimeEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "TimeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkDate",
                table: "TimeEntries",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AlterColumn<string>(
                name: "StripeSubscriptionId",
                table: "Subscriptions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "Subscriptions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlanName",
                table: "Subscriptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyPrice",
                table: "RetainerAgreements",
                type: "decimal(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "RetainerAgreements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "RetainerAgreements",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AlterColumn<decimal>(
                name: "OverageHours",
                table: "OverageApprovals",
                type: "decimal(8,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "OverageAmount",
                table: "OverageApprovals",
                type: "decimal(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Decision",
                table: "OverageApprovals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "OverageApprovals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ClientNameSnapshot",
                table: "Invoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetainerAgreementId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "InvoiceLineItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "InvoiceLineItems",
                type: "decimal(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "ClientContacts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "ClientContacts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InvoiceLineItems",
                table: "InvoiceLineItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClientContacts",
                table: "ClientContacts",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ClientPortalCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActivationTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActivationExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPortalCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HourlyCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_UserId",
                table: "WorkspaceMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_WorkspaceId_UserId",
                table: "WorkspaceMembers",
                columns: new[] { "WorkspaceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_WorkspaceId_WorkDate",
                table: "TimeEntries",
                columns: new[] { "WorkspaceId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_StripeSubscriptionId",
                table: "Subscriptions",
                column: "StripeSubscriptionId",
                unique: true,
                filter: "[StripeSubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_WorkspaceId",
                table: "Subscriptions",
                column: "WorkspaceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainerAgreements_WorkspaceId",
                table: "RetainerAgreements",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_RetainerAgreements_WorkspaceId_ClientId",
                table: "RetainerAgreements",
                columns: new[] { "WorkspaceId", "ClientId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OverageApprovals_InvoiceId",
                table: "OverageApprovals",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_OverageApprovals_WorkspaceId",
                table: "OverageApprovals",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId_Status",
                table: "Invoices",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_WorkspaceId",
                table: "Invoices",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_WorkspaceId_RetainerAgreementId_PeriodStart",
                table: "Invoices",
                columns: new[] { "WorkspaceId", "RetainerAgreementId", "PeriodStart" },
                unique: true,
                filter: "[RetainerAgreementId] IS NOT NULL AND [Status] <> 'Void'");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId_Email",
                table: "ClientContacts",
                columns: new[] { "ClientId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalCredentials_ActivationTokenHash",
                table: "ClientPortalCredentials",
                column: "ActivationTokenHash",
                unique: true,
                filter: "[ActivationTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalCredentials_ClientContactId",
                table: "ClientPortalCredentials",
                column: "ClientContactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientPortalCredentials_WorkspaceId_Email",
                table: "ClientPortalCredentials",
                columns: new[] { "WorkspaceId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_WorkspaceId_Email",
                table: "Invitations",
                columns: new[] { "WorkspaceId", "Email" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientContacts_Clients_ClientId",
                table: "ClientContacts",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientContacts_Clients_ClientId",
                table: "ClientContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                table: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "ClientPortalCredentials");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMembers_UserId",
                table: "WorkspaceMembers");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMembers_WorkspaceId_UserId",
                table: "WorkspaceMembers");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_WorkspaceId_WorkDate",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_StripeSubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_WorkspaceId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_RetainerAgreements_WorkspaceId",
                table: "RetainerAgreements");

            migrationBuilder.DropIndex(
                name: "IX_RetainerAgreements_WorkspaceId_ClientId",
                table: "RetainerAgreements");

            migrationBuilder.DropIndex(
                name: "IX_OverageApprovals_InvoiceId",
                table: "OverageApprovals");

            migrationBuilder.DropIndex(
                name: "IX_OverageApprovals_WorkspaceId",
                table: "OverageApprovals");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ClientId_Status",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_WorkspaceId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_WorkspaceId_RetainerAgreementId_PeriodStart",
                table: "Invoices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InvoiceLineItems",
                table: "InvoiceLineItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClientContacts",
                table: "ClientContacts");

            migrationBuilder.DropIndex(
                name: "IX_ClientContacts_ClientId_Email",
                table: "ClientContacts");

            migrationBuilder.DropColumn(
                name: "RequireTimeApproval",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "HourlyCost",
                table: "WorkspaceMembers");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "RetainerAgreements");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "RetainerAgreements");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "OverageApprovals");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RetainerAgreementId",
                table: "Invoices");

            migrationBuilder.RenameTable(
                name: "InvoiceLineItems",
                newName: "InvoiceLineItem");

            migrationBuilder.RenameTable(
                name: "ClientContacts",
                newName: "ClientContact");

            migrationBuilder.RenameIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItem",
                newName: "IX_InvoiceLineItem_InvoiceId");

            migrationBuilder.AlterColumn<string>(
                name: "StripeSubscriptionId",
                table: "Subscriptions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "Subscriptions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PlanName",
                table: "Subscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyPrice",
                table: "RetainerAgreements",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "OverageHours",
                table: "OverageApprovals",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(8,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "OverageAmount",
                table: "OverageApprovals",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)");

            migrationBuilder.AlterColumn<int>(
                name: "Decision",
                table: "OverageApprovals",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Invoices",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ClientNameSnapshot",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "InvoiceLineItem",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "InvoiceLineItem",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "ClientContact",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "ClientContact",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddPrimaryKey(
                name: "PK_InvoiceLineItem",
                table: "InvoiceLineItem",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClientContact",
                table: "ClientContact",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_WorkspaceId_CreatedAt",
                table: "TimeEntries",
                columns: new[] { "WorkspaceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContact_ClientId",
                table: "ClientContact",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientContact_Clients_ClientId",
                table: "ClientContact",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItem_Invoices_InvoiceId",
                table: "InvoiceLineItem",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
