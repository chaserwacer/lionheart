using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using System.Net;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;

namespace lionheart.Tests.Integration.EndpointsTests;

public class MovementEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testUserId = Guid.NewGuid().ToString();
    private readonly string _otherUserId = Guid.NewGuid().ToString();
    private readonly string _dbName = Guid.NewGuid().ToString();

    public MovementEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot("");
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ModelContext>));
                services.RemoveAll(typeof(DbContextOptions));
                services.RemoveAll(typeof(ModelContext));

                services.AddDbContext<ModelContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });

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

        // Seed users
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

    #region Helpers

    private async Task<Guid> CreateTrainingProgramForUser(string userId, string title = "Program")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", userId);

        var request = new CreateTrainingProgramRequest
        {
            Title = title,
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Test" }
        };

        var response = await _client.PostAsJsonAsync("/api/training-program/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TrainingProgram>();
        return created!.TrainingProgramID;
    }

    private async Task<Guid> CreateTrainingSessionForUser(string userId, Guid programId)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", userId);

        var request = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var response = await _client.PostAsJsonAsync("/api/training-session/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TrainingSessionDTO>();
        return created!.TrainingSessionID;
    }

    private async Task<Guid> CreateMovementBase(string userId, string name = "Squat")
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", userId);

        var request = new CreateMovementBaseRequest { Name = name };
        var response = await _client.PostAsJsonAsync("/api/movement-base/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<MovementBase>();
        return created!.MovementBaseID;
    }

    #endregion

    #region Success Cases

    [Fact]
    public async Task CreateMovement_WithValidData_ReturnsCreatedMovement()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId, "Bench Press");

        var request = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "Good set"
        };

        var response = await _client.PostAsJsonAsync("/api/movement/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<MovementDTO>();
        Assert.NotNull(movement);
        Assert.Equal(sessionId, movement.TrainingSessionID);
        Assert.Equal(movementBaseId, movement.MovementBaseID);
        Assert.Equal("Paused", movement.MovementModifier.Name);
        Assert.Equal("Good set", movement.Notes);
        Assert.False(movement.IsCompleted);
    }

    [Fact]
    public async Task GetMovements_WithNoMovements_ReturnsEmptyList()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);

        var response = await _client.GetAsync($"/api/movement/get-all/{sessionId}");

        response.EnsureSuccessStatusCode();
        var movements = await response.Content.ReadFromJsonAsync<List<MovementDTO>>();
        Assert.NotNull(movements);
        Assert.Empty(movements);
    }

    [Fact]
    public async Task GetMovements_WithMultipleMovements_ReturnsAllMovements()
    {
        _client.DefaultRequestHeaders.Authorization =
           new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId1 = await CreateMovementBase(_testUserId, "Bench Press");
        var movementBaseId2 = await CreateMovementBase(_testUserId, "Squat");

        var request1 = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId1,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "Good set"
        };
        var request2 = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId2,
            MovementModifier = new MovementModifier { Name = "High Bar", Equipment = "Barbell", Duration = 0 },
            Notes = "Solid"
        };

        var response1 = await _client.PostAsJsonAsync("/api/movement/create", request1);
        var response2 = await _client.PostAsJsonAsync("/api/movement/create", request2);

        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response2.StatusCode);

        var movement1 = await response1.Content.ReadFromJsonAsync<MovementDTO>();
        var movement2 = await response2.Content.ReadFromJsonAsync<MovementDTO>();

        var getResponse = await _client.GetAsync($"/api/movement/get-all/{sessionId}");

        getResponse.EnsureSuccessStatusCode();
        var movements = await getResponse.Content.ReadFromJsonAsync<List<MovementDTO>>();
        Assert.NotNull(movements);
        Assert.Contains(movements, m => m.MovementID == movement1!.MovementID);
        Assert.Contains(movements, m => m.MovementID == movement2!.MovementID);
        Assert.Equal(2, movements.Count);
    }

    [Fact]
    public async Task UpdateMovement_WithValidData_ReturnsUpdatedMovement()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId, "Deadlift");

        // Create
        var createRequest = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Conventional", Equipment = "Barbell", Duration = 0 },
            Notes = "Heavy"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/movement/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MovementDTO>();

        // Update
        var updateRequest = new UpdateMovementRequest
        {
            MovementID = created!.MovementID,
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Sumo", Equipment = "Barbell", Duration = 0 },
            Notes = "Switched to sumo",
            IsCompleted = true
        };

        var response = await _client.PutAsJsonAsync("/api/movement/update", updateRequest);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MovementDTO>();
        Assert.NotNull(updated);
        Assert.Equal("Sumo", updated.MovementModifier.Name);
        Assert.Equal("Switched to sumo", updated.Notes);
        Assert.True(updated.IsCompleted);
    }

    [Fact]
    public async Task DeleteMovement_WithValidId_ReturnsNoContent()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId, "Pull-up");

        var createRequest = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Bodyweight", Equipment = "None", Duration = 0 },
            Notes = "Easy"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/movement/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MovementDTO>();

        var response = await _client.DeleteAsync($"/api/movement/delete/{created!.MovementID}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deleted
        var getResponse = await _client.GetAsync($"/api/movement/get/{created.MovementID}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    #endregion

    #region Validation Failure Cases

    [Fact]
    public async Task CreateMovement_WithMissingMovementBaseID_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);

        var request = new
        {
            // MovementBaseID missing
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "Missing MovementBaseID",
            TrainingSessionID = sessionId
        };

        var response = await _client.PostAsJsonAsync("/api/movement/create", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMovement_WithMissingMovementModifier_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);

        var request = new
        {
            MovementBaseID = movementBaseId,
            // MovementModifier missing
            Notes = "Missing MovementModifier",
            TrainingSessionID = sessionId
        };

        var response = await _client.PostAsJsonAsync("/api/movement/create", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMovement_WithMissingNotes_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);

        var request = new
        {
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            // Notes missing
            TrainingSessionID = sessionId
        };

        var response = await _client.PostAsJsonAsync("/api/movement/create", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMovement_WithMissingTrainingSessionID_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var movementBaseId = await CreateMovementBase(_testUserId);

        var request = new
        {
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "Missing TrainingSessionID"
            // TrainingSessionID missing
        };

        var response = await _client.PostAsJsonAsync("/api/movement/create", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMovement_WithMissingMovementID_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);

        var request = new
        {
            // MovementID missing
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "Missing MovementID",
            TrainingSessionID = sessionId,
            IsCompleted = false
        };

        var response = await _client.PutAsJsonAsync("/api/movement/update", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMovement_WithMissingIsCompleted_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);

        // First, create a movement
        var createRequest = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "For update"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/movement/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MovementDTO>();

        // Now, try to update without IsCompleted
        var updateRequest = new
        {
            MovementID = created!.MovementID,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "Missing IsCompleted",
            TrainingSessionID = sessionId
            // IsCompleted missing
        };

        var response = await _client.PutAsJsonAsync("/api/movement/update", updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Not Found Cases


    [Fact]
    public async Task DeleteMovement_WithNonExistentId_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.DeleteAsync($"/api/movement/delete/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CreateMovement_InOtherUsersSession_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);
        // Arrange: _otherUserId owns the program/session, _testUserId tries to add movement
        var programId = await CreateTrainingProgramForUser(_otherUserId, "Other's Program");
        var sessionId = await CreateTrainingSessionForUser(_otherUserId, programId);
        var movementBaseId = await CreateMovementBase(_otherUserId, "Bench Press");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Paused", Equipment = "Barbell", Duration = 2 },
            Notes = "Should not be allowed"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/movement/create", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMovement_InOtherUsersSession_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);
        // Arrange: _otherUserId owns the program/session/movement, _testUserId tries to update
        var programId = await CreateTrainingProgramForUser(_otherUserId, "Other's Program");
        var sessionId = await CreateTrainingSessionForUser(_otherUserId, programId);
        var movementBaseId = await CreateMovementBase(_otherUserId, "Squat");

        // Create movement as _otherUserId
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var createRequest = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "High Bar", Equipment = "Barbell", Duration = 0 },
            Notes = "Other's movement"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/movement/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MovementDTO>();

        // Try to update as _testUserId
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var updateRequest = new UpdateMovementRequest
        {
            MovementID = created!.MovementID,
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Low Bar", Equipment = "Barbell", Duration = 0 },
            Notes = "Trying to hack",
            IsCompleted = true
        };

        var response = await _client.PutAsJsonAsync("/api/movement/update", updateRequest);


        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMovement_InOtherUsersSession_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);
        // Arrange: _otherUserId owns the program/session/movement, _testUserId tries to delete
        var programId = await CreateTrainingProgramForUser(_otherUserId, "Other's Program");
        var sessionId = await CreateTrainingSessionForUser(_otherUserId, programId);
        var movementBaseId = await CreateMovementBase(_otherUserId, "Deadlift");

        // Create movement as _otherUserId
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var createRequest = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Conventional", Equipment = "Barbell", Duration = 0 },
            Notes = "Other's movement"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/movement/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MovementDTO>();

        // Try to delete as _testUserId
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.DeleteAsync($"/api/movement/delete/{created!.MovementID}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }



    [Fact]
    public async Task GetAllMovements_InOtherUsersSession_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);
        // Arrange: _otherUserId owns the program/session, _testUserId tries to get all movements
        var programId = await CreateTrainingProgramForUser(_otherUserId, "Other's Program");
        var sessionId = await CreateTrainingSessionForUser(_otherUserId, programId);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.GetAsync($"/api/movement/get-all/{sessionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateMovement_WithSpecialCharactersInNotes_WorksCorrectly()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId, "Sit-up");

        var request = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Weighted", Equipment = "Plate", Duration = 0 },
            Notes = "🔥💪 Special chars! @#%&*"
        };

        var response = await _client.PostAsJsonAsync("/api/movement/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await response.Content.ReadFromJsonAsync<Movement>();
        Assert.NotNull(movement);
        Assert.Contains("🔥💪", movement.Notes);
    }

    #endregion
}
