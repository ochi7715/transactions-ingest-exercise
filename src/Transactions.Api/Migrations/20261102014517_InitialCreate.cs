using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transactions.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Scenario = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystem = table.Column<string>(type: "TEXT", maxLength: 19, nullable: false),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardNumber = table.Column<string>(type: "TEXT", maxLength: 19, nullable: false),
                    LocationCode = table.Column<string>(type: "TEXT", maxLength: 19, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 19, nullable: false),
                    AmountInCents = table.Column<long>(type: "INTEGER", nullable: false),
                    TransactionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transaction_Amount", "AmountInCents BETWEEN -999999999999999999 AND 999999999999999999");
                    table.CheckConstraint("CK_Transaction_Status", "Status IN ('Active', 'Revoked')");
                    table.CheckConstraint("CK_Transaction_Strings", "length(SourceSystem) BETWEEN 1 AND 19 AND length(CardNumber) < 20 AND length(LocationCode) BETWEEN 1 AND 19 AND length(ProductName) BETWEEN 1 AND 19");
                });

            migrationBuilder.CreateTable(
                name: "ImportSourceResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportRunId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceSystem = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Inserted = table.Column<int>(type: "INTEGER", nullable: false),
                    Updated = table.Column<int>(type: "INTEGER", nullable: false),
                    Revoked = table.Column<int>(type: "INTEGER", nullable: false),
                    Restored = table.Column<int>(type: "INTEGER", nullable: false),
                    Unchanged = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSourceResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportSourceResults_ImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "ImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionRecordId = table.Column<long>(type: "INTEGER", nullable: false),
                    ImportRunId = table.Column<long>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditChanges_ImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "ImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditChanges_Transactions_TransactionRecordId",
                        column: x => x.TransactionRecordId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditChanges_ImportRunId",
                table: "AuditChanges",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditChanges_TransactionRecordId_Id",
                table: "AuditChanges",
                columns: new[] { "TransactionRecordId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportSourceResults_ImportRunId",
                table: "ImportSourceResults",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourceSystem_TransactionId",
                table: "Transactions",
                columns: new[] { "SourceSystem", "TransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditChanges");

            migrationBuilder.DropTable(
                name: "ImportSourceResults");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "ImportRuns");
        }
    }
}
