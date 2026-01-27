using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lionheart.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "LionheartUsers",
                columns: table => new
                {
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdentityUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Age = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LionheartUsers", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_LionheartUsers_Users_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TimeInMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CaloriesBurned = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserSummary = table.Column<string>(type: "TEXT", nullable: false),
                    PerceivedEffortRatings_RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PerceivedEffortRatings_AccumulatedFatigue = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_DifficultyRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_EngagementRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_ExternalVariablesRating = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.ActivityID);
                    table.ForeignKey(
                        name: "FK_Activities_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiAccessTokens",
                columns: table => new
                {
                    ObjectID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationName = table.Column<string>(type: "TEXT", nullable: false),
                    PersonalAccessToken = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiAccessTokens", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_ApiAccessTokens_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LionheartUserUserID = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.ChatConversationID);
                    table.ForeignKey(
                        name: "FK_ChatConversations_LionheartUsers_LionheartUserUserID",
                        column: x => x.LionheartUserUserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "DailyOuraDatas",
                columns: table => new
                {
                    ObjectID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SyncDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ResilienceData_SleepRecovery = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_DaytimeRecovery = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_Stress = table.Column<double>(type: "REAL", nullable: false),
                    ResilienceData_ResilienceLevel = table.Column<string>(type: "TEXT", nullable: false),
                    ActivityData_ActivityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_Steps = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_ActiveCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TotalCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TargetCalories = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_MeetDailyTargets = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_MoveEveryHour = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_RecoveryTime = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_StayActive = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TrainingFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityData_TrainingVolume = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_SleepScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_DeepSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Efficiency = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Latency = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_RemSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Restfulness = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_Timing = table.Column<int>(type: "INTEGER", nullable: false),
                    SleepData_TotalSleep = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_ReadinessScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_TemperatureDeviation = table.Column<double>(type: "REAL", nullable: false),
                    ReadinessData_ActivityBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_BodyTemperature = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_HrvBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_PreviousDayActivity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_PreviousNight = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_RecoveryIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_RestingHeartRate = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadinessData_SleepBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivityJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResilienceJson = table.Column<string>(type: "TEXT", nullable: false),
                    SleepJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReadinessJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyOuraDatas", x => x.ObjectID);
                    table.ForeignKey(
                        name: "FK_DailyOuraDatas_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Equipments",
                columns: table => new
                {
                    EquipmentID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipments", x => x.EquipmentID);
                    table.ForeignKey(
                        name: "FK_Equipments_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Injuries",
                columns: table => new
                {
                    InjuryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    InjuryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Injuries", x => x.InjuryID);
                    table.ForeignKey(
                        name: "FK_Injuries_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovementBases",
                columns: table => new
                {
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementBases", x => x.MovementBaseID);
                    table.ForeignKey(
                        name: "FK_MovementBases_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovementModifiers",
                columns: table => new
                {
                    MovementModifierID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementModifiers", x => x.MovementModifierID);
                    table.ForeignKey(
                        name: "FK_MovementModifiers_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingPrograms",
                columns: table => new
                {
                    TrainingProgramID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPrograms", x => x.TrainingProgramID);
                    table.ForeignKey(
                        name: "FK_TrainingPrograms_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WellnessStates",
                columns: table => new
                {
                    StateID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MotivationScore = table.Column<int>(type: "INTEGER", nullable: false),
                    StressScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MoodScore = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergyScore = table.Column<int>(type: "INTEGER", nullable: false),
                    OverallScore = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WellnessStates", x => x.StateID);
                    table.ForeignKey(
                        name: "FK_WellnessStates_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_ModelChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_SystemChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolCallID = table.Column<string>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_ToolChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserChatMessages",
                columns: table => new
                {
                    ChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatConversationID = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChatMessages", x => x.ChatMessageItemID);
                    table.ForeignKey(
                        name: "FK_UserChatMessages_ChatConversations_ChatConversationID",
                        column: x => x.ChatConversationID,
                        principalTable: "ChatConversations",
                        principalColumn: "ChatConversationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InjuryEvents",
                columns: table => new
                {
                    InjuryEventID = table.Column<Guid>(type: "TEXT", nullable: false),
                    InjuryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    PainLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    MovementIDs = table.Column<string>(type: "TEXT", nullable: false),
                    InjuryType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InjuryEvents", x => x.InjuryEventID);
                    table.ForeignKey(
                        name: "FK_InjuryEvents_Injuries_InjuryID",
                        column: x => x.InjuryID,
                        principalTable: "Injuries",
                        principalColumn: "InjuryID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MuscleGroups",
                columns: table => new
                {
                    MuscleGroupID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MuscleGroups", x => x.MuscleGroupID);
                    table.ForeignKey(
                        name: "FK_MuscleGroups_MovementBases_MovementBaseID",
                        column: x => x.MovementBaseID,
                        principalTable: "MovementBases",
                        principalColumn: "MovementBaseID");
                });

            migrationBuilder.CreateTable(
                name: "MovementDatas",
                columns: table => new
                {
                    MovementDataID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    EquipmentID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementBaseID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementModifierID = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementDatas", x => x.MovementDataID);
                    table.ForeignKey(
                        name: "FK_MovementDatas_Equipments_EquipmentID",
                        column: x => x.EquipmentID,
                        principalTable: "Equipments",
                        principalColumn: "EquipmentID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementDatas_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovementDatas_MovementBases_MovementBaseID",
                        column: x => x.MovementBaseID,
                        principalTable: "MovementBases",
                        principalColumn: "MovementBaseID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementDatas_MovementModifiers_MovementModifierID",
                        column: x => x.MovementModifierID,
                        principalTable: "MovementModifiers",
                        principalColumn: "MovementModifierID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingProgramID = table.Column<Guid>(type: "TEXT", nullable: true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    PerceivedEffortRatings_RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PerceivedEffortRatings_AccumulatedFatigue = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_DifficultyRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_EngagementRating = table.Column<int>(type: "INTEGER", nullable: true),
                    PerceivedEffortRatings_ExternalVariablesRating = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.TrainingSessionID);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_TrainingPrograms_TrainingProgramID",
                        column: x => x.TrainingProgramID,
                        principalTable: "TrainingPrograms",
                        principalColumn: "TrainingProgramID");
                });

            migrationBuilder.CreateTable(
                name: "ChatToolCall",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    LHModelChatMessageChatMessageItemID = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatToolCall", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatToolCall_ModelChatMessages_LHModelChatMessageChatMessageItemID",
                        column: x => x.LHModelChatMessageChatMessageItemID,
                        principalTable: "ModelChatMessages",
                        principalColumn: "ChatMessageItemID");
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingSessionID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementDataID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ordering = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.MovementID);
                    table.ForeignKey(
                        name: "FK_Movements_MovementDatas_MovementDataID",
                        column: x => x.MovementDataID,
                        principalTable: "MovementDatas",
                        principalColumn: "MovementDataID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_TrainingSessions_TrainingSessionID",
                        column: x => x.TrainingSessionID,
                        principalTable: "TrainingSessions",
                        principalColumn: "TrainingSessionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DTSetEntries",
                columns: table => new
                {
                    SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecommendedDistance = table.Column<double>(type: "REAL", nullable: false),
                    ActualDistance = table.Column<double>(type: "REAL", nullable: false),
                    IntervalDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    TargetPace = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ActualPace = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RecommendedDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ActualDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RecommendedRest = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ActualRest = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    IntervalType = table.Column<int>(type: "INTEGER", nullable: false),
                    DistanceUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualRPE = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DTSetEntries", x => x.SetEntryID);
                    table.ForeignKey(
                        name: "FK_DTSetEntries_Movements_MovementID",
                        column: x => x.MovementID,
                        principalTable: "Movements",
                        principalColumn: "MovementID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiftSetEntries",
                columns: table => new
                {
                    SetEntryID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementID = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecommendedReps = table.Column<int>(type: "INTEGER", nullable: true),
                    RecommendedWeight = table.Column<double>(type: "REAL", nullable: true),
                    RecommendedRPE = table.Column<double>(type: "REAL", nullable: true),
                    ActualReps = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualWeight = table.Column<double>(type: "REAL", nullable: false),
                    ActualRPE = table.Column<double>(type: "REAL", nullable: false),
                    WeightUnit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiftSetEntries", x => x.SetEntryID);
                    table.ForeignKey(
                        name: "FK_LiftSetEntries_Movements_MovementID",
                        column: x => x.MovementID,
                        principalTable: "Movements",
                        principalColumn: "MovementID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonalRecords",
                columns: table => new
                {
                    PersonalRecordID = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserID = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementDataID = table.Column<Guid>(type: "TEXT", nullable: false),
                    PRType = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    WeightUnit = table.Column<int>(type: "INTEGER", nullable: false),
                    Reps = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PreviousPRCreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousPersonalRecordID = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceLiftSetEntryID = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalRecords", x => x.PersonalRecordID);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_LiftSetEntries_SourceLiftSetEntryID",
                        column: x => x.SourceLiftSetEntryID,
                        principalTable: "LiftSetEntries",
                        principalColumn: "SetEntryID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_LionheartUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "LionheartUsers",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_MovementDatas_MovementDataID",
                        column: x => x.MovementDataID,
                        principalTable: "MovementDatas",
                        principalColumn: "MovementDataID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalRecords_PersonalRecords_PreviousPersonalRecordID",
                        column: x => x.PreviousPersonalRecordID,
                        principalTable: "PersonalRecords",
                        principalColumn: "PersonalRecordID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserID",
                table: "Activities",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ApiAccessTokens_UserID",
                table: "ApiAccessTokens",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_LionheartUserUserID",
                table: "ChatConversations",
                column: "LionheartUserUserID");

            migrationBuilder.CreateIndex(
                name: "IX_ChatToolCall_LHModelChatMessageChatMessageItemID",
                table: "ChatToolCall",
                column: "LHModelChatMessageChatMessageItemID");

            migrationBuilder.CreateIndex(
                name: "IX_DailyOuraDatas_UserID",
                table: "DailyOuraDatas",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_DTSetEntries_MovementID",
                table: "DTSetEntries",
                column: "MovementID");

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_UserID",
                table: "Equipments",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Injuries_UserID",
                table: "Injuries",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_InjuryEvents_InjuryID",
                table: "InjuryEvents",
                column: "InjuryID");

            migrationBuilder.CreateIndex(
                name: "IX_LiftSetEntries_MovementID",
                table: "LiftSetEntries",
                column: "MovementID");

            migrationBuilder.CreateIndex(
                name: "IX_LionheartUsers_IdentityUserId",
                table: "LionheartUsers",
                column: "IdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelChatMessages_ChatConversationID",
                table: "ModelChatMessages",
                column: "ChatConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementBases_UserID",
                table: "MovementBases",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_EquipmentID",
                table: "MovementDatas",
                column: "EquipmentID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_MovementBaseID",
                table: "MovementDatas",
                column: "MovementBaseID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_MovementModifierID",
                table: "MovementDatas",
                column: "MovementModifierID");

            migrationBuilder.CreateIndex(
                name: "IX_MovementDatas_UserID_EquipmentID_MovementBaseID_MovementModifierID",
                table: "MovementDatas",
                columns: new[] { "UserID", "EquipmentID", "MovementBaseID", "MovementModifierID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovementModifiers_UserID",
                table: "MovementModifiers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_MovementDataID",
                table: "Movements",
                column: "MovementDataID");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_TrainingSessionID",
                table: "Movements",
                column: "TrainingSessionID");

            migrationBuilder.CreateIndex(
                name: "IX_MuscleGroups_MovementBaseID",
                table: "MuscleGroups",
                column: "MovementBaseID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_MovementDataID",
                table: "PersonalRecords",
                column: "MovementDataID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_PreviousPersonalRecordID",
                table: "PersonalRecords",
                column: "PreviousPersonalRecordID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_SourceLiftSetEntryID",
                table: "PersonalRecords",
                column: "SourceLiftSetEntryID");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalRecords_UserID_MovementDataID_PRType_IsActive",
                table: "PersonalRecords",
                columns: new[] { "UserID", "MovementDataID", "PRType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemChatMessages_ChatConversationID",
                table: "SystemChatMessages",
                column: "ChatConversationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolChatMessages_ChatConversationID",
                table: "ToolChatMessages",
                column: "ChatConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPrograms_UserID",
                table: "TrainingPrograms",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_TrainingProgramID",
                table: "TrainingSessions",
                column: "TrainingProgramID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_UserID",
                table: "TrainingSessions",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatMessages_ChatConversationID",
                table: "UserChatMessages",
                column: "ChatConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_WellnessStates_UserID",
                table: "WellnessStates",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "ApiAccessTokens");

            migrationBuilder.DropTable(
                name: "ChatToolCall");

            migrationBuilder.DropTable(
                name: "DailyOuraDatas");

            migrationBuilder.DropTable(
                name: "DTSetEntries");

            migrationBuilder.DropTable(
                name: "InjuryEvents");

            migrationBuilder.DropTable(
                name: "MuscleGroups");

            migrationBuilder.DropTable(
                name: "PersonalRecords");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "SystemChatMessages");

            migrationBuilder.DropTable(
                name: "ToolChatMessages");

            migrationBuilder.DropTable(
                name: "UserChatMessages");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "WellnessStates");

            migrationBuilder.DropTable(
                name: "ModelChatMessages");

            migrationBuilder.DropTable(
                name: "Injuries");

            migrationBuilder.DropTable(
                name: "LiftSetEntries");

            migrationBuilder.DropTable(
                name: "ChatConversations");

            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "MovementDatas");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "Equipments");

            migrationBuilder.DropTable(
                name: "MovementBases");

            migrationBuilder.DropTable(
                name: "MovementModifiers");

            migrationBuilder.DropTable(
                name: "TrainingPrograms");

            migrationBuilder.DropTable(
                name: "LionheartUsers");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
