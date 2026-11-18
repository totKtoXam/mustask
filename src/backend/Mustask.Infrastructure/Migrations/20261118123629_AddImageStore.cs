using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mustask.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImageStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "images");

            migrationBuilder.CreateTable(
                name: "images",
                schema: "images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    byte_length = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    frame_count = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bound_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_images", x => x.id);
                    table.CheckConstraint("ck_images_byte_length", "byte_length BETWEEN 1 AND 5242880");
                    table.CheckConstraint("ck_images_dimensions", "(width IS NULL OR width BETWEEN 1 AND 20000) AND (height IS NULL OR height BETWEEN 1 AND 20000) AND frame_count BETWEEN 1 AND 1000");
                    table.CheckConstraint("ck_images_format", "format IN ('Png', 'Jpeg', 'Gif', 'Webp')");
                    table.CheckConstraint("ck_images_purpose_owner", "purpose = 'WorkItemDescription' AND work_item_id IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_images_work_items_work_item_id",
                        column: x => x.work_item_id,
                        principalTable: "work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "image_contents",
                schema: "images",
                columns: table => new
                {
                    image_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_image_contents", x => x.image_id);
                    table.ForeignKey(
                        name: "fk_image_contents_images_image_id",
                        column: x => x.image_id,
                        principalSchema: "images",
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ADR-0045 decision 1: out-of-line and uncompressed (a picture is already compressed; reads slice nothing).
            migrationBuilder.Sql("ALTER TABLE images.image_contents ALTER COLUMN content SET STORAGE EXTERNAL");

            migrationBuilder.CreateIndex(
                name: "ix_images_unbound_uploaded_at",
                schema: "images",
                table: "images",
                columns: new[] { "uploaded_at", "id" },
                filter: "bound_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_images_work_item_id",
                schema: "images",
                table: "images",
                column: "work_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "image_contents",
                schema: "images");

            migrationBuilder.DropTable(
                name: "images",
                schema: "images");

            migrationBuilder.DropSchema(
                name: "images");
        }
    }
}
