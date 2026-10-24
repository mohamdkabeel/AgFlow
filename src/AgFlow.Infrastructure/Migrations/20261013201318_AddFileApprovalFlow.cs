using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileApprovalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileVersion_FileAssetId",
                table: "FileVersion");

            migrationBuilder.AlterColumn<string>(
                name: "BlobPath",
                table: "FileVersion",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "FileAssets",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ApprovalStatus",
                table: "FileAssets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "FileAssets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "FileAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByContactId",
                table: "FileAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileVersion_FileAssetId_VersionNumber",
                table: "FileVersion",
                columns: new[] { "FileAssetId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_WorkspaceId_ApprovalStatus",
                table: "FileAssets",
                columns: new[] { "WorkspaceId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_WorkspaceId_TaskItemId",
                table: "FileAssets",
                columns: new[] { "WorkspaceId", "TaskItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileVersion_FileAssetId_VersionNumber",
                table: "FileVersion");

            migrationBuilder.DropIndex(
                name: "IX_FileAssets_WorkspaceId_ApprovalStatus",
                table: "FileAssets");

            migrationBuilder.DropIndex(
                name: "IX_FileAssets_WorkspaceId_TaskItemId",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "ReviewedByContactId",
                table: "FileAssets");

            migrationBuilder.AlterColumn<string>(
                name: "BlobPath",
                table: "FileVersion",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "FileAssets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(260)",
                oldMaxLength: 260);

            migrationBuilder.AlterColumn<int>(
                name: "ApprovalStatus",
                table: "FileAssets",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_FileVersion_FileAssetId",
                table: "FileVersion",
                column: "FileAssetId");
        }
    }
}
