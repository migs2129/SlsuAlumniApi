using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlsuAlumniApi.Migrations
{
    /// <inheritdoc />
    public partial class AddExamResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Month = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    DataSource = table.Column<string>(type: "TEXT", nullable: false),
                    SlsuPassers = table.Column<int>(type: "INTEGER", nullable: false),
                    SlsuExaminees = table.Column<int>(type: "INTEGER", nullable: false),
                    SlsuPassingRate = table.Column<double>(type: "REAL", nullable: false),
                    FirstTimePassers = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstTimeExaminees = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstTimePassingRate = table.Column<double>(type: "REAL", nullable: false),
                    RepeaterPassers = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeaterExaminees = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeaterPassingRate = table.Column<double>(type: "REAL", nullable: false),
                    NationalPassers = table.Column<int>(type: "INTEGER", nullable: false),
                    NationalExaminees = table.Column<int>(type: "INTEGER", nullable: false),
                    NationalPassingRate = table.Column<double>(type: "REAL", nullable: false),
                    DifferenceFromNational = table.Column<double>(type: "REAL", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamResults", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$RRg1ix7KqCO/rr.tbJR12eiTqeUZIEp5r4cgInJSmRuN1juYp5Ssq");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamResults");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$fLfRNKVEkCYpUPILDJI7KePm5yFlls5HViGw3WliSdfN9l4blUuhe");
        }
    }
}
