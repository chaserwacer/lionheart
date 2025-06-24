using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OpenApi.Extensions;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace lionheart.Services
{
    /// <summary>
    /// Service for managing wellness states.
    /// </summary>
    [McpServerToolType]
    public class WellnessService : IWellnessService
    {
        private readonly ModelContext _context;

        public WellnessService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<WellnessState>> GetWellnessStateAsync(IdentityUser user, DateOnly date)
        {
            var userGuid = Guid.Parse(user.Id);
            var state = await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == date && w.UserID == userGuid) ?? new WellnessState(userGuid, 1, 1, 1, 1, date) { OverallScore = -1 };
            return Result<WellnessState>.Success(state);
        }
        public async Task<Result<List<WellnessState>>> GetWellnessStatesAsync(IdentityUser user, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(user.Id);
            DateOnly startDate = dateRange.StartDate;
            DateOnly endDate = dateRange.EndDate;

            var states = await _context.WellnessStates
                .Where(w => w.Date >= startDate && w.Date <= endDate && w.UserID == userGuid)
                .ToListAsync();

            return Result<List<WellnessState>>.Success(states.OrderBy(w => w.Date).ToList());
        }

        public async Task<Result<WellnessState>> AddWellnessStateAsync(IdentityUser user, CreateWellnessStateRequest req)
        {
            var userGuid = Guid.Parse(user.Id);
            DateOnly selectedDate = DateOnly.ParseExact(req.Date, "yyyy-MM-dd");

            // Check if already exists
            var existingState = await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == selectedDate && w.UserID == userGuid);
            if (existingState != null)
            {
                existingState.EnergyScore = req.Energy;
                existingState.MoodScore = req.Mood;
                existingState.StressScore = req.Stress;
                existingState.MotivationScore = req.Motivation;
                double total = (double)(req.Mood + req.Energy + req.Motivation + (6 - req.Stress));
                int decimalPlaces = 2;
                int numComponents = 4;
                existingState.OverallScore = Math.Round(total / numComponents, decimalPlaces);
                await _context.SaveChangesAsync();
                return Result<WellnessState>.Success(existingState);
            }
            else
            {
                WellnessState wellnessState = new WellnessState(userGuid, req.Motivation, req.Stress, req.Mood, req.Energy, selectedDate);
                _context.WellnessStates.Add(wellnessState);
                await _context.SaveChangesAsync();
                return Result<WellnessState>.Created(wellnessState);
            }
        }
        [McpServerTool, Description("Gets the wellness state for the user from the given range")]
        public async Task<List<WellnessState>> GetWellnessStatesMCP(string userID, DateRangeRequest dateRange)
        {
            var userGuid = Guid.Parse(userID);
            DateOnly startDate = dateRange.StartDate;
            DateOnly endDate = dateRange.EndDate;

            var states = await _context.WellnessStates
                .Where(w => w.Date >= startDate && w.Date <= endDate && w.UserID == userGuid)
                .ToListAsync();

            return states.OrderBy(w => w.Date).ToList();
        }
        [McpServerTool, Description("Adds or updates a wellness state for the user for the given date")]
        public async Task<Result<WellnessState>> AddWellnessStateMCPV(string userID, CreateWellnessStateRequest req)
        {
            var userGuid = Guid.Parse(userID);
            DateOnly selectedDate = DateOnly.ParseExact(req.Date, "yyyy-MM-dd");

            // Check if already exists
            var existingState = await _context.WellnessStates.FirstOrDefaultAsync(w => w.Date == selectedDate && w.UserID == userGuid);
            if (existingState != null)
            {
                existingState.EnergyScore = req.Energy;
                existingState.MoodScore = req.Mood;
                existingState.StressScore = req.Stress;
                existingState.MotivationScore = req.Motivation;
                double total = (double)(req.Mood + req.Energy + req.Motivation + (6 - req.Stress));
                int decimalPlaces = 2;
                int numComponents = 4;
                existingState.OverallScore = Math.Round(total / numComponents, decimalPlaces);
                await _context.SaveChangesAsync();
                return Result<WellnessState>.Success(existingState);
            }
            else
            {
                WellnessState wellnessState = new WellnessState(userGuid, req.Motivation, req.Stress, req.Mood, req.Energy, selectedDate);
                _context.WellnessStates.Add(wellnessState);
                await _context.SaveChangesAsync();
                return Result<WellnessState>.Created(wellnessState);
            }
        }
    }
}
