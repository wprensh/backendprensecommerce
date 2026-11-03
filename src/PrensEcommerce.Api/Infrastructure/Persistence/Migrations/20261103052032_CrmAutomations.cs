using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrensEcommerce.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CrmAutomations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_automation_runs_tenant_id_status",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.AddColumn<string>(
                name: "dedup_key",
                schema: "crm",
                table: "automation_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_at",
                schema: "crm",
                table: "automation_runs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "crm",
                table: "automation_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cart_activities",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_cart_activities_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "core",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cart_activities_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_events",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "core",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customer_events_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_segments",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rules = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_segments", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_segments_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "core",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_automation_runs_tenant_id_dedup_key",
                schema: "crm",
                table: "automation_runs",
                columns: new[] { "tenant_id", "dedup_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_automation_runs_tenant_id_status_scheduled_at",
                schema: "crm",
                table: "automation_runs",
                columns: new[] { "tenant_id", "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_automation_runs_tenant_id_user_id",
                schema: "crm",
                table: "automation_runs",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_automation_runs_user_id",
                schema: "crm",
                table: "automation_runs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_activities_tenant_id",
                schema: "crm",
                table: "cart_activities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_cart_activities_tenant_id_item_count_last_activity_at",
                schema: "crm",
                table: "cart_activities",
                columns: new[] { "tenant_id", "item_count", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cart_activities_tenant_id_user_id",
                schema: "crm",
                table: "cart_activities",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_activities_user_id",
                schema: "crm",
                table: "cart_activities",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_events_tenant_id",
                schema: "crm",
                table: "customer_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_events_tenant_id_processed_at",
                schema: "crm",
                table: "customer_events",
                columns: new[] { "tenant_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_events_tenant_id_user_id_occurred_at",
                schema: "crm",
                table: "customer_events",
                columns: new[] { "tenant_id", "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_events_user_id",
                schema: "crm",
                table: "customer_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_segments_tenant_id",
                schema: "crm",
                table: "customer_segments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_segments_tenant_id_name",
                schema: "crm",
                table: "customer_segments",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_automation_runs_users_user_id",
                schema: "crm",
                table: "automation_runs",
                column: "user_id",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_automation_runs_users_user_id",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.DropTable(
                name: "cart_activities",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customer_events",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customer_segments",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "ix_automation_runs_tenant_id_dedup_key",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.DropIndex(
                name: "ix_automation_runs_tenant_id_status_scheduled_at",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.DropIndex(
                name: "ix_automation_runs_tenant_id_user_id",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.DropIndex(
                name: "ix_automation_runs_user_id",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.DropColumn(
                name: "dedup_key",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.DropColumn(
                name: "scheduled_at",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "crm",
                table: "automation_runs");

            migrationBuilder.CreateIndex(
                name: "ix_automation_runs_tenant_id_status",
                schema: "crm",
                table: "automation_runs",
                columns: new[] { "tenant_id", "status" });
        }
    }
}
