﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using lionheart.Data;

#nullable disable

namespace lionheart.Migrations
{
    [DbContext(typeof(ModelContext))]
    partial class ModelContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.7");

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("ConcurrencyStamp")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedName")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Roles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClaimType")
                        .HasColumnType("TEXT");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("TEXT");

                    b.Property<string>("RoleId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("RoleClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ConcurrencyStamp")
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .HasColumnType("TEXT");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedEmail")
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedUserName")
                        .HasColumnType("TEXT");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("TEXT");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("TEXT");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("TEXT");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UserName")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClaimType")
                        .HasColumnType("TEXT");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("UserClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("LoginProvider")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderKey")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("UserId");

                    b.ToTable("UserLogins");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("RoleId")
                        .HasColumnType("TEXT");

                    b.HasKey("UserId", "RoleId");

                    b.ToTable("UserRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("LoginProvider")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.HasKey("UserId");

                    b.ToTable("UserTokens");
                });

            modelBuilder.Entity("lionheart.ActivityTracking.Activity", b =>
                {
                    b.Property<Guid>("ActivityID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<int>("AccumulatedFatigue")
                        .HasColumnType("INTEGER");

                    b.Property<int>("CaloriesBurned")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("DateTime")
                        .HasColumnType("TEXT");

                    b.Property<int>("DifficultyRating")
                        .HasColumnType("INTEGER");

                    b.Property<int>("EngagementRating")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ExternalVariablesRating")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("TimeInMinutes")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("UserID")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserSummary")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("ActivityID");

                    b.HasIndex("UserID");

                    b.ToTable("Activities");
                });

            modelBuilder.Entity("lionheart.ActivityTracking.LiftDetails", b =>
                {
                    b.Property<Guid>("ActivityID")
                        .HasColumnType("TEXT");

                    b.Property<int>("BackSets")
                        .HasColumnType("INTEGER");

                    b.Property<int>("BicepSets")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ChestSets")
                        .HasColumnType("INTEGER");

                    b.Property<int>("HamstringSets")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LiftFocus")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LiftType")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("QuadSets")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ShoulderSets")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Tonnage")
                        .HasColumnType("INTEGER");

                    b.Property<int>("TricepSets")
                        .HasColumnType("INTEGER");

                    b.HasKey("ActivityID");

                    b.ToTable("LiftDetails");
                });

            modelBuilder.Entity("lionheart.ActivityTracking.RideDetails", b =>
                {
                    b.Property<Guid>("ActivityID")
                        .HasColumnType("TEXT");

                    b.Property<int>("AveragePower")
                        .HasColumnType("INTEGER");

                    b.Property<double>("AverageSpeed")
                        .HasColumnType("REAL");

                    b.Property<double>("Distance")
                        .HasColumnType("REAL");

                    b.Property<int>("ElevationGain")
                        .HasColumnType("INTEGER");

                    b.Property<string>("RideType")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("ActivityID");

                    b.ToTable("RideDetails");
                });

            modelBuilder.Entity("lionheart.ActivityTracking.RunWalkDetails", b =>
                {
                    b.Property<Guid>("ActivityID")
                        .HasColumnType("TEXT");

                    b.Property<int>("AveragePaceInSeconds")
                        .HasColumnType("INTEGER");

                    b.Property<double>("Distance")
                        .HasColumnType("REAL");

                    b.Property<int>("ElevationGain")
                        .HasColumnType("INTEGER");

                    b.Property<string>("MileSplitsInSeconds")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("RunType")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("ActivityID");

                    b.ToTable("RunWalkDetails");
                });

            modelBuilder.Entity("lionheart.WellBeing.LionheartUser", b =>
                {
                    b.Property<Guid>("UserID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<int>("Age")
                        .HasColumnType("INTEGER");

                    b.Property<string>("IdentityUserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<float>("Weight")
                        .HasColumnType("REAL");

                    b.HasKey("UserID");

                    b.HasIndex("IdentityUserId");

                    b.ToTable("LionheartUsers");
                });

            modelBuilder.Entity("lionheart.WellBeing.WellnessState", b =>
                {
                    b.Property<Guid>("StateID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<DateOnly>("Date")
                        .HasColumnType("TEXT");

                    b.Property<int>("EnergyScore")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MoodScore")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MotivationScore")
                        .HasColumnType("INTEGER");

                    b.Property<double>("OverallScore")
                        .HasColumnType("REAL");

                    b.Property<int>("StressScore")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("UserID")
                        .HasColumnType("TEXT");

                    b.HasKey("StateID");

                    b.HasIndex("UserID");

                    b.ToTable("WellnessStates");
                });

            modelBuilder.Entity("lionheart.ActivityTracking.Activity", b =>
                {
                    b.HasOne("lionheart.WellBeing.LionheartUser", null)
                        .WithMany("Activities")
                        .HasForeignKey("UserID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("lionheart.ActivityTracking.LiftDetails", b =>
                {
                    b.HasOne("lionheart.ActivityTracking.Activity", null)
                        .WithOne("LiftDetails")
                        .HasForeignKey("lionheart.ActivityTracking.LiftDetails", "ActivityID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("lionheart.ActivityTracking.RideDetails", b =>
                {
                    b.HasOne("lionheart.ActivityTracking.Activity", null)
                        .WithOne("RideDetails")
                        .HasForeignKey("lionheart.ActivityTracking.RideDetails", "ActivityID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("lionheart.ActivityTracking.RunWalkDetails", b =>
                {
                    b.HasOne("lionheart.ActivityTracking.Activity", null)
                        .WithOne("RunWalkDetails")
                        .HasForeignKey("lionheart.ActivityTracking.RunWalkDetails", "ActivityID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("lionheart.WellBeing.LionheartUser", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityUser", "IdentityUser")
                        .WithMany()
                        .HasForeignKey("IdentityUserId");

                    b.Navigation("IdentityUser");
                });

            modelBuilder.Entity("lionheart.WellBeing.WellnessState", b =>
                {
                    b.HasOne("lionheart.WellBeing.LionheartUser", null)
                        .WithMany("WellnessStates")
                        .HasForeignKey("UserID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("lionheart.ActivityTracking.Activity", b =>
                {
                    b.Navigation("LiftDetails");

                    b.Navigation("RideDetails");

                    b.Navigation("RunWalkDetails");
                });

            modelBuilder.Entity("lionheart.WellBeing.LionheartUser", b =>
                {
                    b.Navigation("Activities");

                    b.Navigation("WellnessStates");
                });
#pragma warning restore 612, 618
        }
    }
}
