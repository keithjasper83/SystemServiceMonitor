using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemServiceMonitor.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    DesiredState = table.Column<int>(type: "INTEGER", nullable: false),
                    ObservedState = table.Column<int>(type: "INTEGER", nullable: false),
                    HealthState = table.Column<int>(type: "INTEGER", nullable: false),
                    RepairState = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoRepairEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartCommand = table.Column<string>(type: "TEXT", nullable: true),
                    StopCommand = table.Column<string>(type: "TEXT", nullable: true),
                    RestartCommand = table.Column<string>(type: "TEXT", nullable: true),
                    HealthcheckCommand = table.Column<string>(type: "TEXT", nullable: true),
                    WorkingDirectory = table.Column<string>(type: "TEXT", nullable: true),
                    EnvironmentVariables = table.Column<string>(type: "TEXT", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresElevation = table.Column<bool>(type: "INTEGER", nullable: false),
                    GitHubRepoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    GitHubBranch = table.Column<string>(type: "TEXT", nullable: true),
                    DeployedCommitHash = table.Column<string>(type: "TEXT", nullable: true),
                    WslDistroName = table.Column<string>(type: "TEXT", nullable: true),
                    DockerIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    DependencyIds = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Resources");
        }
    }
}
