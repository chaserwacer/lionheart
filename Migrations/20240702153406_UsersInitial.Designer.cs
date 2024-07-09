﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using lionheart.Data;

#nullable disable

namespace lionheart.Migrations
{
    [DbContext(typeof(ModelContext))]
    [Migration("20240702153406_UsersInitial")]
    partial class UsersInitial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.6");

            modelBuilder.Entity("lionheart.WellBeing.User", b =>
                {
                    b.Property<Guid>("UserID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<int>("Age")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<float>("Weight")
                        .HasColumnType("REAL");

                    b.HasKey("UserID");

                    b.ToTable("Users");
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

                    b.Property<int>("FatigueScore")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MoodScore")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MotivationScore")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("UserID")
                        .HasColumnType("TEXT");

                    b.Property<string>("WrittenDescription")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("StateID");

                    b.HasIndex("UserID");

                    b.ToTable("WellnessStates");
                });

            modelBuilder.Entity("lionheart.WellBeing.WellnessState", b =>
                {
                    b.HasOne("lionheart.WellBeing.User", null)
                        .WithMany("WellnessStates")
                        .HasForeignKey("UserID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("lionheart.WellBeing.User", b =>
                {
                    b.Navigation("WellnessStates");
                });
#pragma warning restore 612, 618
        }
    }
}
