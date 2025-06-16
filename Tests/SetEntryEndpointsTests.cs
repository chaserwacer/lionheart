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

public class SetEntryEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testUserId = Guid.NewGuid().ToString();
    private readonly string _otherUserId = Guid.NewGuid().ToString();
    private readonly string _dbName = Guid.NewGuid().ToString();

    public SetEntryEndpointsTests(WebApplicationFactory<Program> factory)
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

    private async Task<Guid> CreateTrainingProgramForUser(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", userId);

        var request = new CreateTrainingProgramRequest
        {
            Title = "Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Test" }
        };

        var response = await client.PostAsJsonAsync("/api/training-program/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TrainingProgram>();
        return created!.TrainingProgramID;
    }

    private async Task<Guid> CreateTrainingSessionForUser(string userId, Guid programId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", userId);

        var request = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var response = await client.PostAsJsonAsync("/api/training-session/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TrainingSessionDTO>();
        return created!.TrainingSessionID;
    }

    private async Task<Guid> CreateMovementBase(string userId, string name = "Squat")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", userId);

        var request = new CreateMovementBaseRequest { Name = name };
        var response = await client.PostAsJsonAsync("/api/movement-base/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<MovementBase>();
        return created!.MovementBaseID;
    }

    private async Task<Guid> CreateMovementForUser(string userId, Guid sessionId, Guid movementBaseId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", userId);

        var request = new CreateMovementRequest
        {
            TrainingSessionID = sessionId,
            MovementBaseID = movementBaseId,
            MovementModifier = new MovementModifier { Name = "Standard", Equipment = "Barbell", Duration = 0 },
            Notes = "Test movement"
        };

        var response = await client.PostAsJsonAsync("/api/movement/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<MovementDTO>();
        return created!.MovementID;
    }

    #endregion

    #region Success Cases

    [Fact]
    public async Task CreateSetEntry_WithValidData_ReturnsCreatedSetEntry()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);
        var movementId = await CreateMovementForUser(_testUserId, sessionId, movementBaseId);

        var request = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };

        var response = await _client.PostAsJsonAsync("/api/set-entry/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var setEntry = await response.Content.ReadFromJsonAsync<SetEntryDTO>();
        Assert.NotNull(setEntry);
        Assert.Equal(request.MovementID, setEntry.MovementID);
        Assert.Equal(request.RecommendedReps, setEntry.RecommendedReps);
        Assert.Equal(request.RecommendedWeight, setEntry.RecommendedWeight);
        Assert.Equal(request.RecommendedRPE, setEntry.RecommendedRPE);
        Assert.Equal(request.WeightUnit, setEntry.WeightUnit);
        Assert.Equal(request.ActualReps, setEntry.ActualReps);
        Assert.Equal(request.ActualWeight, setEntry.ActualWeight);
        Assert.Equal(request.ActualRPE, setEntry.ActualRPE);
    }

    [Fact]
    public async Task UpdateSetEntry_WithValidData_ReturnsUpdatedSetEntry()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);
        var movementId = await CreateMovementForUser(_testUserId, sessionId, movementBaseId);

        // Create set entry
        var createRequest = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };
        var createResponse = await _client.PostAsJsonAsync("/api/set-entry/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SetEntryDTO>();

        // Update set entry
        var updateRequest = new UpdateSetEntryRequest
        {
            SetEntryID = created!.SetEntryID,
            MovementID = movementId,
            RecommendedReps = 6,
            RecommendedWeight = 105,
            RecommendedRPE = 9,
            WeightUnit = WeightUnit.Pounds,
            ActualReps = 6,
            ActualWeight = 105,
            ActualRPE = 9
        };

        var response = await _client.PutAsJsonAsync("/api/set-entry/update", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SetEntryDTO>();
        Assert.NotNull(updated);
        Assert.Equal(updateRequest.SetEntryID, updated.SetEntryID);
        Assert.Equal(updateRequest.RecommendedReps, updated.RecommendedReps);
        Assert.Equal(updateRequest.WeightUnit, updated.WeightUnit);
        Assert.Equal(updateRequest.ActualRPE, updated.ActualRPE);
    }

    [Fact]
    public async Task DeleteSetEntry_WithValidId_ReturnsNoContent()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);
        var movementId = await CreateMovementForUser(_testUserId, sessionId, movementBaseId);

        // Create set entry
        var createRequest = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };
        var createResponse = await _client.PostAsJsonAsync("/api/set-entry/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SetEntryDTO>();

        // Delete set entry
        var response = await _client.DeleteAsync($"/api/set-entry/delete/{created!.SetEntryID}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    }

    #endregion

    #region Validation Failure Cases

    [Fact]
    public async Task CreateSetEntry_WithMissingFields_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new
        {
            // Missing all required fields
        };

        var response = await _client.PostAsJsonAsync("/api/set-entry/create", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSetEntry_WithInvalidRPE_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);
        var movementId = await CreateMovementForUser(_testUserId, sessionId, movementBaseId);

        var request = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 11, // Invalid, should be 1-10
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };

        var response = await _client.PostAsJsonAsync("/api/set-entry/create", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSetEntry_WithMissingSetEntryID_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new
        {
            // SetEntryID missing
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8,
            MovementID = Guid.NewGuid()
        };

        var response = await _client.PutAsJsonAsync("/api/set-entry/update", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Not Found Cases

    [Fact]
    public async Task UpdateSetEntry_WithNonExistentId_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);
        var movementId = await CreateMovementForUser(_testUserId, sessionId, movementBaseId);

        var updateRequest = new UpdateSetEntryRequest
        {
            SetEntryID = Guid.NewGuid(), // Non-existent
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };

        var response = await _client.PutAsJsonAsync("/api/set-entry/update", updateRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSetEntry_WithNonExistentId_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.DeleteAsync($"/api/set-entry/delete/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CreateSetEntry_InOtherUsersMovement_ReturnsNotFound()
    {
        // Arrange: _otherUserId owns the program/session/movement, _testUserId tries to add set entry
        var programId = await CreateTrainingProgramForUser(_otherUserId);
        var sessionId = await CreateTrainingSessionForUser(_otherUserId, programId);
        var movementBaseId = await CreateMovementBase(_otherUserId);
        var movementId = await CreateMovementForUser(_otherUserId, sessionId, movementBaseId);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };

        var response = await _client.PostAsJsonAsync("/api/set-entry/create", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSetEntry_InOtherUsersMovement_ReturnsNotFound()
    {
        // Arrange: _otherUserId owns the program/session/movement/setentry, _testUserId tries to update
        var programId = await CreateTrainingProgramForUser(_otherUserId);
        var sessionId = await CreateTrainingSessionForUser(_otherUserId, programId);
        var movementBaseId = await CreateMovementBase(_otherUserId);
        var movementId = await CreateMovementForUser(_otherUserId, sessionId, movementBaseId);

        // Create set entry as other user
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var createRequest = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };
        var createResponse = await client.PostAsJsonAsync("/api/set-entry/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SetEntryDTO>();

        // Try to update as test user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var updateRequest = new UpdateSetEntryRequest
        {
            SetEntryID = created!.SetEntryID,
            MovementID = movementId,
            RecommendedReps = 6,
            RecommendedWeight = 105,
            RecommendedRPE = 9,
            WeightUnit = WeightUnit.Pounds,
            ActualReps = 6,
            ActualWeight = 105,
            ActualRPE = 9
        };

        var response = await _client.PutAsJsonAsync("/api/set-entry/update", updateRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSetEntry_InOtherUsersMovement_ReturnsNotFound()
    {
        // Arrange: _otherUserId owns the program/session/movement/setentry, _testUserId tries to delete
        var programId = await CreateTrainingProgramForUser(_otherUserId);
        var sessionId = await CreateTrainingSessionForUser(_otherUserId, programId);
        var movementBaseId = await CreateMovementBase(_otherUserId);
        var movementId = await CreateMovementForUser(_otherUserId, sessionId, movementBaseId);

        // Create set entry as other user
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var createRequest = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 5,
            RecommendedWeight = 100,
            RecommendedRPE = 8,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 5,
            ActualWeight = 100,
            ActualRPE = 8
        };
        var createResponse = await client.PostAsJsonAsync("/api/set-entry/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<SetEntryDTO>();

        // Try to delete as test user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.DeleteAsync($"/api/set-entry/delete/{created!.SetEntryID}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task UnauthenticatedDeleteRequest_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/set-entry/delete/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateSetEntry_WithZeroWeight_WorksCorrectly()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);
        var movementId = await CreateMovementForUser(_testUserId, sessionId, movementBaseId);

        var request = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 10,
            RecommendedWeight = 0,
            RecommendedRPE = 5,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 10,
            ActualWeight = 0,
            ActualRPE = 5
        };

        var response = await _client.PostAsJsonAsync("/api/set-entry/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var setEntry = await response.Content.ReadFromJsonAsync<SetEntryDTO>();
        Assert.Equal(0, setEntry!.RecommendedWeight);
        Assert.Equal(0, setEntry.ActualWeight);
    }

    [Fact]
    public async Task CreateSetEntry_WithSpecialValuesInReps_WorksCorrectly()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var sessionId = await CreateTrainingSessionForUser(_testUserId, programId);
        var movementBaseId = await CreateMovementBase(_testUserId);
        var movementId = await CreateMovementForUser(_testUserId, sessionId, movementBaseId);

        var request = new CreateSetEntryRequest
        {
            MovementID = movementId,
            RecommendedReps = 1,
            RecommendedWeight = 50,
            RecommendedRPE = 1,
            WeightUnit = WeightUnit.Kilograms,
            ActualReps = 100,
            ActualWeight = 50,
            ActualRPE = 10
        };

        var response = await _client.PostAsJsonAsync("/api/set-entry/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var setEntry = await response.Content.ReadFromJsonAsync<SetEntryDTO>();
        Assert.Equal(1, setEntry!.RecommendedReps);
        Assert.Equal(100, setEntry.ActualReps);
        Assert.Equal(1, setEntry.RecommendedRPE);
        Assert.Equal(10, setEntry.ActualRPE);
    }

    #endregion
}
