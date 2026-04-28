using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taller.Migrations
{
    /// <inheritdoc />
    public partial class InicialMVC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vehiculos",
                columns: table => new
                {
                    Patente = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Marca = table.Column<string>(type: "TEXT", nullable: false),
                    Modelo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehiculos", x => x.Patente);
                });

            migrationBuilder.CreateTable(
                name: "Reparaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Patente = table.Column<string>(type: "TEXT", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kilometraje = table.Column<int>(type: "INTEGER", nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reparaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reparaciones_Vehiculos_Patente",
                        column: x => x.Patente,
                        principalTable: "Vehiculos",
                        principalColumn: "Patente",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reparaciones_Patente",
                table: "Reparaciones",
                column: "Patente");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reparaciones");

            migrationBuilder.DropTable(
                name: "Vehiculos");
        }
    }
}
