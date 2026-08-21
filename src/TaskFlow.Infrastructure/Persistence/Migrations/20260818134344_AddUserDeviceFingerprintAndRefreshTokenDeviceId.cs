using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeviceFingerprintAndRefreshTokenDeviceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDevices_UserId",
                table: "UserDevices");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<string>(
                name: "DeviceFingerprint",
                table: "UserDevices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "UserDevices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UserDeviceId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_UserId_DeviceFingerprint",
                table: "UserDevices",
                columns: new[] { "UserId", "DeviceFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserDeviceId",
                table: "RefreshTokens",
                column: "UserDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_UserDeviceId",
                table: "RefreshTokens",
                columns: new[] { "UserId", "UserDeviceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_UserDevices_UserDeviceId",
                table: "RefreshTokens",
                column: "UserDeviceId",
                principalTable: "UserDevices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_UserDevices_UserDeviceId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserDevices_UserId_DeviceFingerprint",
                table: "UserDevices");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserDeviceId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_UserDeviceId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "DeviceFingerprint",
                table: "UserDevices");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "UserDevices");

            migrationBuilder.DropColumn(
                name: "UserDeviceId",
                table: "RefreshTokens");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_UserId",
                table: "UserDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");
        }
    }
}
