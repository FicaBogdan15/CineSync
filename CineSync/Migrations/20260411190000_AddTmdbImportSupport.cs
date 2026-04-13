using CineSync.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CineSync.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260411190000_AddTmdbImportSupport")]
    public partial class AddTmdbImportSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TmdbPersonId",
                table: "Directors",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                table: "Movies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TmdbPersonId",
                table: "Actors",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Directors_TmdbPersonId",
                table: "Directors",
                column: "TmdbPersonId",
                unique: true,
                filter: "[TmdbPersonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_TmdbId",
                table: "Movies",
                column: "TmdbId",
                unique: true,
                filter: "[TmdbId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Actors_TmdbPersonId",
                table: "Actors",
                column: "TmdbPersonId",
                unique: true,
                filter: "[TmdbPersonId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Directors_TmdbPersonId",
                table: "Directors");

            migrationBuilder.DropIndex(
                name: "IX_Movies_TmdbId",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Actors_TmdbPersonId",
                table: "Actors");

            migrationBuilder.DropColumn(
                name: "TmdbPersonId",
                table: "Directors");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "TmdbPersonId",
                table: "Actors");
        }
    }
}
