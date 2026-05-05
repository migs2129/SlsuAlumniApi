using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SlsuAlumniApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTopNotchersColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TopNotchers",
                table: "ExamResults",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$FDfdk.GBjQnlDCXZi0KsW.PL/g47OHQbW4IyExI.bhr/sSS9agWwO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TopNotchers",
                table: "ExamResults");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$RRg1ix7KqCO/rr.tbJR12eiTqeUZIEp5r4cgInJSmRuN1juYp5Ssq");
        }
    }
}
