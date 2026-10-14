using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhabzelaConsulting.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaxProfileAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                table: "Notifications",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompany",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmployer",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProvisionalTaxpayer",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatRegistered",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VatCategory",
                table: "Clients",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "YearEndMonth",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ClientId_DedupKey",
                table: "Notifications",
                columns: new[] { "ClientId", "DedupKey" },
                unique: true,
                filter: "[DedupKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_ClientId_DedupKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsCompany",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsEmployer",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsProvisionalTaxpayer",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsVatRegistered",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VatCategory",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "YearEndMonth",
                table: "Clients");
        }
    }
}
