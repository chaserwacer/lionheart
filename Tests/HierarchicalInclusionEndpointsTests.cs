using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;
using lionheart.Model.TrainingProgram;
using lionheart.Model.DTOs;
using Xunit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using lionheart.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using FluentAssertions;

namespace lionheart.Tests.Integration.EndpointsTests;

public class HierarchicalInclusionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testUserId = Guid.NewGuid().ToString();
    private readonly string _otherUserId = Guid.NewGuid().ToString();
    private readonly string _dbName = Guid.NewGuid().ToString(); // Shared DB name

    public HierarchicalInclusionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot("");
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Remove existing context registrations
                services.RemoveAll(typeof(DbContextOptions<ModelContext>));
                services.RemoveAll(typeof(DbContextOptions));
                services.RemoveAll(typeof(ModelContext));

                // Use a shared in-memory database
                services.AddDbContext<ModelContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });

                // Add test authentication and set as default
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationSchemeHandler>("Test", options => { });

                services.Configure<IdentityOptions>(options =>
                {
                    options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
                });

                services.ConfigureApplicationCookie(options =>
                {
                    options.Cookie.Name = "TestAuth";
                    options.LoginPath = "/";
                    options.AccessDeniedPath = "/";
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = 403;
                        return Task.CompletedTask;
                    };
                });
            });
        });
          // Now seed users using the factory's service provider (which uses the shared DB)
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (userManager.FindByIdAsync(_testUserId).Result == null)
            {
                userManager.CreateAsync(new IdentityUser { Id = _testUserId, UserName = _testUserId, Email = _testUserId }).Wait();
            }
            if (userManager.FindByIdAsync(_otherUserId).Result == null)
            {
                userManager.CreateAsync(new IdentityUser { Id = _otherUserId, UserName = _otherUserId, Email = _otherUserId }).Wait();
            }
        }

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetTrainingProgramAndSession_IncludeAllChildren_HierarchyIsReturned()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        // Create Training Program
        var programRequest = new CreateTrainingProgramRequest
        {
            Title = "Hierarchy Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Hierarchy" }
        };
        var programResponse = await _client.PostAsJsonAsync("/api/training-program/create", programRequest);
        programResponse.EnsureSuccessStatusCode();
        var program = await programResponse.Content.ReadFromJsonAsync<TrainingProgramDTO>();
        Assert.NotNull(program);

        // Create two Sessions
        var sessionIds = new List<Guid>();
        for (int i = 0; i < 2; i++)
        {
            var sessionRequest = new CreateTrainingSessionRequest
            {
                TrainingProgramID = program.TrainingProgramID,
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(i))
            };
            var sessionResponse = await _client.PostAsJsonAsync("/api/training-session/create", sessionRequest);
            sessionResponse.EnsureSuccessStatusCode();
            var session = await sessionResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();
            Assert.NotNull(session);
            sessionIds.Add(session.TrainingSessionID);
        }

        // Create MovementBase
        var movementBaseRequest = new CreateMovementBaseRequest { Name = "Bench Press" };
        var movementBaseResponse = await _client.PostAsJsonAsync("/api/movement-base/create", movementBaseRequest);
        movementBaseResponse.EnsureSuccessStatusCode();
        var movementBase = await movementBaseResponse.Content.ReadFromJsonAsync<MovementBase>();
        Assert.NotNull(movementBase);

        // For each session, add a movement with a set entry
        var movementIds = new List<Guid>();
        foreach (var sessionId in sessionIds)
        {
            var movementRequest = new CreateMovementRequest
            {
                TrainingSessionID = sessionId,
                MovementBaseID = movementBase.MovementBaseID,
                MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
                Notes = $"Movement for session {sessionId}",
                WeightUnit = WeightUnit.Kilograms,
            };
            var movementResponse = await _client.PostAsJsonAsync("/api/movement/create", movementRequest);
            movementResponse.EnsureSuccessStatusCode();
            var movement = await movementResponse.Content.ReadFromJsonAsync<MovementDTO>();
            Assert.NotNull(movement);
            movementIds.Add(movement.MovementID);

            // Add a set entry to the movement
            var setEntryRequest = new CreateSetEntryRequest
            {
                MovementID = movement.MovementID,
                RecommendedReps = 5,
                RecommendedWeight = 100,
                RecommendedRPE = 8,
                ActualReps = 5,
                ActualWeight = 100,
                ActualRPE = 8
            };
            var setEntryResponse = await _client.PostAsJsonAsync("/api/set-entry/create", setEntryRequest);
            setEntryResponse.EnsureSuccessStatusCode();
            var setEntry = await setEntryResponse.Content.ReadFromJsonAsync<SetEntryDTO>();
            Assert.NotNull(setEntry);
        }

        // Act & Assert: Fetch the program and check sessions and movements
        var getProgramResponse = await _client.GetAsync($"/api/training-program/get/{program.TrainingProgramID}");
        getProgramResponse.EnsureSuccessStatusCode();
        var fetchedProgram = await getProgramResponse.Content.ReadFromJsonAsync<TrainingProgramDTO>();
        Assert.NotNull(fetchedProgram);
        Assert.NotNull(fetchedProgram.TrainingSessions);
        Assert.Equal(2, fetchedProgram.TrainingSessions.Count);

        foreach (var session in fetchedProgram.TrainingSessions)
        {
            Assert.Contains(session.TrainingSessionID, sessionIds);
            Assert.NotNull(session.Movements);
            Assert.Single(session.Movements);

            var movement = session.Movements.First();
            Assert.Contains(movement.MovementID, movementIds);
            Assert.Equal(movementBase.MovementBaseID, movement.MovementBaseID);
            Assert.Equal(session.TrainingSessionID, movement.TrainingSessionID);
            Assert.NotNull(movement.Sets);
            Assert.Single(movement.Sets);

            var setEntry = movement.Sets.First();
            Assert.Equal(5, setEntry.RecommendedReps);
            Assert.Equal(100, setEntry.RecommendedWeight);
        }

        // Act & Assert: Fetch each session and check movements and set entries
        foreach (var sessionId in sessionIds)
        {
            var getSessionResponse = await _client.GetAsync($"/api/training-session/get/{sessionId}");
            getSessionResponse.EnsureSuccessStatusCode();
            var fetchedSession = await getSessionResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();
            Assert.NotNull(fetchedSession);
            Assert.NotNull(fetchedSession.Movements);
            Assert.Single(fetchedSession.Movements);

            var movement = fetchedSession.Movements.First();
            Assert.Contains(movement.MovementID, movementIds);
            Assert.NotNull(movement.Sets);
            Assert.Single(movement.Sets);

            var setEntry = movement.Sets.First();
            Assert.Equal(5, setEntry.RecommendedReps);
            Assert.Equal(100, setEntry.RecommendedWeight);
        }
        var fetchedSessions = await _client.GetAsync($"/api/training-session/get-all/{fetchedProgram.TrainingProgramID}");
        fetchedSessions.EnsureSuccessStatusCode();
        var fetchedProgramSessions = await fetchedSessions.Content.ReadFromJsonAsync<List<TrainingSessionDTO>>();
        if (fetchedProgramSessions == null)
        {
            Assert.Fail("Fetched program sessions should not be null");
        }

        //Assert.Equal(fetchedProgram.TrainingSessions, fetchedProgramSessions);
        fetchedProgram.TrainingSessions.Should().BeEquivalentTo(fetchedProgramSessions);
    }
}