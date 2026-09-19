using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupHierarchyCorporateFieldsAndWorkflowExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPermanentId",
                table: "Shareholders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PermanentIdDate",
                table: "Shareholders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ParentGroupId",
                table: "ShareholderGroups",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BoardResolutionDate",
                table: "ShareholderApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfIncorporation",
                table: "ShareholderApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NatureOfBusiness",
                table: "ShareholderApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidUpCapital",
                table: "ShareholderApplications",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceOfFund",
                table: "ShareholderApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeOfInstitution",
                table: "ShareholderApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NrcNumber",
                table: "CorporateSignatories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialAddress",
                table: "CorporateSignatories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BoardResolutionDate",
                table: "Corporates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfIncorporation",
                table: "Corporates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NatureOfBusiness",
                table: "Corporates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidUpCapital",
                table: "Corporates",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceOfFund",
                table: "Corporates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeOfInstitution",
                table: "Corporates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicationAuthorizedSigners",
                columns: table => new
                {
                    ApplicationAuthorizedSignerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShareholderApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NrcNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationAuthorizedSigners", x => x.ApplicationAuthorizedSignerId);
                    table.ForeignKey(
                        name: "FK_ApplicationAuthorizedSigners_ShareholderApplications_ShareholderApplicationId",
                        column: x => x.ShareholderApplicationId,
                        principalTable: "ShareholderApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationBeneficialOwners",
                columns: table => new
                {
                    ApplicationBeneficialOwnerId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShareholderApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NrcNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnershipPercentage = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationBeneficialOwners", x => x.ApplicationBeneficialOwnerId);
                    table.ForeignKey(
                        name: "FK_ApplicationBeneficialOwners_ShareholderApplications_ShareholderApplicationId",
                        column: x => x.ShareholderApplicationId,
                        principalTable: "ShareholderApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationDirectors",
                columns: table => new
                {
                    ApplicationDirectorId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShareholderApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NrcNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResidentialAddress = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDirectors", x => x.ApplicationDirectorId);
                    table.ForeignKey(
                        name: "FK_ApplicationDirectors_ShareholderApplications_ShareholderApplicationId",
                        column: x => x.ShareholderApplicationId,
                        principalTable: "ShareholderApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareholderGroups_ParentGroupId",
                table: "ShareholderGroups",
                column: "ParentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationAuthorizedSigners_ShareholderApplicationId",
                table: "ApplicationAuthorizedSigners",
                column: "ShareholderApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationBeneficialOwners_ShareholderApplicationId",
                table: "ApplicationBeneficialOwners",
                column: "ShareholderApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDirectors_ShareholderApplicationId",
                table: "ApplicationDirectors",
                column: "ShareholderApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShareholderGroups_ShareholderGroups_ParentGroupId",
                table: "ShareholderGroups",
                column: "ParentGroupId",
                principalTable: "ShareholderGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShareholderGroups_ShareholderGroups_ParentGroupId",
                table: "ShareholderGroups");

            migrationBuilder.DropTable(
                name: "ApplicationAuthorizedSigners");

            migrationBuilder.DropTable(
                name: "ApplicationBeneficialOwners");

            migrationBuilder.DropTable(
                name: "ApplicationDirectors");

            migrationBuilder.DropIndex(
                name: "IX_ShareholderGroups_ParentGroupId",
                table: "ShareholderGroups");

            migrationBuilder.DropColumn(
                name: "IsPermanentId",
                table: "Shareholders");

            migrationBuilder.DropColumn(
                name: "PermanentIdDate",
                table: "Shareholders");

            migrationBuilder.DropColumn(
                name: "ParentGroupId",
                table: "ShareholderGroups");

            migrationBuilder.DropColumn(
                name: "BoardResolutionDate",
                table: "ShareholderApplications");

            migrationBuilder.DropColumn(
                name: "DateOfIncorporation",
                table: "ShareholderApplications");

            migrationBuilder.DropColumn(
                name: "NatureOfBusiness",
                table: "ShareholderApplications");

            migrationBuilder.DropColumn(
                name: "PaidUpCapital",
                table: "ShareholderApplications");

            migrationBuilder.DropColumn(
                name: "SourceOfFund",
                table: "ShareholderApplications");

            migrationBuilder.DropColumn(
                name: "TypeOfInstitution",
                table: "ShareholderApplications");

            migrationBuilder.DropColumn(
                name: "NrcNumber",
                table: "CorporateSignatories");

            migrationBuilder.DropColumn(
                name: "ResidentialAddress",
                table: "CorporateSignatories");

            migrationBuilder.DropColumn(
                name: "BoardResolutionDate",
                table: "Corporates");

            migrationBuilder.DropColumn(
                name: "DateOfIncorporation",
                table: "Corporates");

            migrationBuilder.DropColumn(
                name: "NatureOfBusiness",
                table: "Corporates");

            migrationBuilder.DropColumn(
                name: "PaidUpCapital",
                table: "Corporates");

            migrationBuilder.DropColumn(
                name: "SourceOfFund",
                table: "Corporates");

            migrationBuilder.DropColumn(
                name: "TypeOfInstitution",
                table: "Corporates");
        }
    }
}
