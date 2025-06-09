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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace lionheart.Services
{
    /// <summary>
    /// Service for managing wellness states.
    /// </summary>
    public class WellnessService : IWellnessService
    {
        private readonly ModelContext _context;

        public WellnessService(ModelContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Fetch a tuple containing the list of overall wellness scores and the list of dates for those scores for the last X days. Uses the GetLastXWellnessStatesAsync method.
        /// </summary>
        /// <param name="userID">Identity User username</param>
        /// <param name="date">last date we want info for</param>
        /// <param name="X">number days prior to date</param>
        /// <returns></returns>
        // public async Task<(List<double>, List<string>)> GetLastXWellnessStatesGraphDataAsync(string userID, DateOnly date, int X)
        // {
        //     var states = await GetLastXWellnessStatesAsync(userID, date, X);
        //     if (states is not null)
        //     {
        //         var scoreList = states.Select(s => s.OverallScore).ToList();
        //         var dateList = states.Select(s => s.Date.DayOfWeek.GetDisplayName() + " (" + s.Date.ToString("MM/dd") + ")").ToList();

        //         return (scoreList, dateList);
        //     }
        //     return ([], []);
        // }

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
    }
}
