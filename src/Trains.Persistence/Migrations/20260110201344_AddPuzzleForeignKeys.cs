using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trains.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPuzzleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PuzzleVotes_UserId",
                table: "PuzzleVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_CreatedByUserId",
                table: "Puzzles",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Puzzles_AspNetUsers_CreatedByUserId",
                table: "Puzzles",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PuzzleSolves_AspNetUsers_UserId",
                table: "PuzzleSolves",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PuzzleSolves_Puzzles_PuzzleId",
                table: "PuzzleSolves",
                column: "PuzzleId",
                principalTable: "Puzzles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PuzzleVotes_AspNetUsers_UserId",
                table: "PuzzleVotes",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PuzzleVotes_Puzzles_PuzzleId",
                table: "PuzzleVotes",
                column: "PuzzleId",
                principalTable: "Puzzles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Puzzles_AspNetUsers_CreatedByUserId",
                table: "Puzzles");

            migrationBuilder.DropForeignKey(
                name: "FK_PuzzleSolves_AspNetUsers_UserId",
                table: "PuzzleSolves");

            migrationBuilder.DropForeignKey(
                name: "FK_PuzzleSolves_Puzzles_PuzzleId",
                table: "PuzzleSolves");

            migrationBuilder.DropForeignKey(
                name: "FK_PuzzleVotes_AspNetUsers_UserId",
                table: "PuzzleVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PuzzleVotes_Puzzles_PuzzleId",
                table: "PuzzleVotes");

            migrationBuilder.DropIndex(
                name: "IX_PuzzleVotes_UserId",
                table: "PuzzleVotes");

            migrationBuilder.DropIndex(
                name: "IX_Puzzles_CreatedByUserId",
                table: "Puzzles");
        }
    }
}
