using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace dockertest.Database.Sys.Migrations
{
    /// <inheritdoc />
    public partial class InitialSys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sys_menu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_menu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sys_user",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sys_user", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_sys_menu_Code",
                table: "sys_menu",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_Email",
                table: "sys_user",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sys_user_Username",
                table: "sys_user",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sys_menu");

            migrationBuilder.DropTable(
                name: "sys_user");
        }
    }
}
