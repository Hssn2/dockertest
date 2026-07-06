using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace dockertest.Database.Sys.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHasDataSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "sys_menu",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "sys_menu",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "sys_menu",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "sys_user",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "sys_user",
                keyColumn: "Id",
                keyValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "sys_menu",
                columns: new[] { "Id", "Code", "CreatedAt", "IsActive", "SortOrder", "Title" },
                values: new object[,]
                {
                    { 1, "home", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 1, "Ana Sayfa" },
                    { 2, "update", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 2, "Güncelleme" },
                    { 3, "games", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, "Oyunlar" }
                });

            migrationBuilder.InsertData(
                table: "sys_user",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "Email", "IsActive", "Username" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Yönetici", "admin@dockertest.local", true, "admin" },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Demo Kullanıcı", "demo@dockertest.local", true, "demo" }
                });
        }
    }
}
