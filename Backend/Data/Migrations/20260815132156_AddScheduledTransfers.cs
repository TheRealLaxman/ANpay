using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANpay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTransactionPinSet",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TransactionPinHash",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScheduledTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecurrenceType = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    NextExecutionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecutionCount = table.Column<int>(type: "int", nullable: false),
                    MaxExecutions = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledTransfers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledTransfers_Wallets_DestinationWalletId",
                        column: x => x.DestinationWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledTransfers_Wallets_SourceWalletId",
                        column: x => x.SourceWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTransfers_DestinationWalletId",
                table: "ScheduledTransfers",
                column: "DestinationWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTransfers_NextExecutionDate",
                table: "ScheduledTransfers",
                column: "NextExecutionDate");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTransfers_SourceWalletId",
                table: "ScheduledTransfers",
                column: "SourceWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTransfers_Status",
                table: "ScheduledTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTransfers_UserId",
                table: "ScheduledTransfers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTransfers");

            migrationBuilder.DropColumn(
                name: "IsTransactionPinSet",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TransactionPinHash",
                table: "AspNetUsers");
        }
    }
}
