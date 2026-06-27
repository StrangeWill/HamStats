using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntennaLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    End = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntennaLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Antennas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Antennas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CallsignPrefixes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prefix = table.Column<string>(type: "TEXT", nullable: false),
                    Grid = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    Continent = table.Column<string>(type: "TEXT", nullable: true),
                    IsExact = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallsignPrefixes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Callsigns",
                columns: table => new
                {
                    Callsign = table.Column<string>(type: "TEXT", nullable: false),
                    Grid = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Region = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Callsigns", x => x.Callsign);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Radio = table.Column<string>(type: "TEXT", nullable: false),
                    Operator = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "N1MMContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FromCall = table.Column<string>(type: "TEXT", nullable: false),
                    ToCall = table.Column<string>(type: "TEXT", nullable: false),
                    Band = table.Column<string>(type: "TEXT", nullable: false),
                    RxFrequency = table.Column<string>(type: "TEXT", nullable: false),
                    TxFrequency = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    CountryPrefix = table.Column<string>(type: "TEXT", nullable: true),
                    Sent = table.Column<string>(type: "TEXT", nullable: true),
                    Receive = table.Column<string>(type: "TEXT", nullable: true),
                    Exchange = table.Column<string>(type: "TEXT", nullable: true),
                    Section = table.Column<string>(type: "TEXT", nullable: true),
                    Operator = table.Column<string>(type: "TEXT", nullable: true),
                    N1MMId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_N1MMContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Radios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Operator = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Radios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Contest = table.Column<string>(type: "TEXT", nullable: true),
                    Call = table.Column<string>(type: "TEXT", nullable: true),
                    Ops = table.Column<string>(type: "TEXT", nullable: true),
                    Power = table.Column<string>(type: "TEXT", nullable: true),
                    Assisted = table.Column<string>(type: "TEXT", nullable: true),
                    Transmitter = table.Column<string>(type: "TEXT", nullable: true),
                    Bands = table.Column<string>(type: "TEXT", nullable: true),
                    Mode = table.Column<string>(type: "TEXT", nullable: true),
                    Overlay = table.Column<string>(type: "TEXT", nullable: true),
                    Club = table.Column<string>(type: "TEXT", nullable: true),
                    DxccCountry = table.Column<string>(type: "TEXT", nullable: true),
                    CqZone = table.Column<string>(type: "TEXT", nullable: true),
                    Iaruzone = table.Column<string>(type: "TEXT", nullable: true),
                    ArrlSection = table.Column<string>(type: "TEXT", nullable: true),
                    Stprvoth = table.Column<string>(type: "TEXT", nullable: true),
                    Grid6 = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RadioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    N1MMContactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FromCall = table.Column<string>(type: "TEXT", nullable: false),
                    ToCall = table.Column<string>(type: "TEXT", nullable: false),
                    Band = table.Column<string>(type: "TEXT", nullable: false),
                    RxFrequency = table.Column<string>(type: "TEXT", nullable: false),
                    TxFrequency = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: true),
                    Class = table.Column<string>(type: "TEXT", nullable: true),
                    Section = table.Column<string>(type: "TEXT", nullable: true),
                    Gridsquare = table.Column<string>(type: "TEXT", nullable: true),
                    Operator = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_N1MMContacts_N1MMContactId",
                        column: x => x.N1MMContactId,
                        principalTable: "N1MMContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Contacts_Radios_RadioId",
                        column: x => x.RadioId,
                        principalTable: "Radios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VFO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RadioId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    RxFrequency = table.Column<string>(type: "TEXT", nullable: true),
                    TxFrequency = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VFO", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VFO_Radios_RadioId",
                        column: x => x.RadioId,
                        principalTable: "Radios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScoreBreakdowns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScoreId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Band = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    QSOs = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreBreakdowns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreBreakdowns_Scores_ScoreId",
                        column: x => x.ScoreId,
                        principalTable: "Scores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "N1MMRadios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VFOId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StationName = table.Column<string>(type: "TEXT", nullable: false),
                    RadioName = table.Column<string>(type: "TEXT", nullable: false),
                    RadioNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RxFrequency = table.Column<string>(type: "TEXT", nullable: true),
                    TxFrequency = table.Column<string>(type: "TEXT", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_N1MMRadios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_N1MMRadios_VFO_VFOId",
                        column: x => x.VFOId,
                        principalTable: "VFO",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallsignPrefixes_Prefix",
                table: "CallsignPrefixes",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_N1MMContactId",
                table: "Contacts",
                column: "N1MMContactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_RadioId",
                table: "Contacts",
                column: "RadioId");

            migrationBuilder.CreateIndex(
                name: "IX_N1MMRadios_VFOId",
                table: "N1MMRadios",
                column: "VFOId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreBreakdowns_ScoreId",
                table: "ScoreBreakdowns",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_VFO_RadioId",
                table: "VFO",
                column: "RadioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntennaLogs");

            migrationBuilder.DropTable(
                name: "Antennas");

            migrationBuilder.DropTable(
                name: "CallsignPrefixes");

            migrationBuilder.DropTable(
                name: "Callsigns");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "N1MMRadios");

            migrationBuilder.DropTable(
                name: "ScoreBreakdowns");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "N1MMContacts");

            migrationBuilder.DropTable(
                name: "VFO");

            migrationBuilder.DropTable(
                name: "Scores");

            migrationBuilder.DropTable(
                name: "Radios");
        }
    }
}
