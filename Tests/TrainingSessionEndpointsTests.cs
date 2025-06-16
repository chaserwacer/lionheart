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

public class TrainingSessionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testUserId = Guid.NewGuid().ToString();
    private readonly string _otherUserId = Guid.NewGuid().ToString();
    private readonly string _dbName = Guid.NewGuid().ToString();

    public TrainingSessionEndpointsTests(WebApplicationFactory<Program> factory)
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

        var response = await client.PostAsJsonAsync("/api/training-program/create", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<TrainingProgramDTO>();
        return created!.TrainingProgramID;
    }

    #endregion

    #region Success Cases

    [Fact]
    public async Task CreateTrainingSession_WithValidData_ReturnsCreatedSession()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var request = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var response = await _client.PostAsJsonAsync("/api/training-session/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<TrainingSessionDTO>();
        Assert.NotNull(dto);
        Assert.Equal(programId, dto.TrainingProgramID);
        Assert.Equal(request.Date, dto.Date);
        Assert.Equal(TrainingSessionStatus.Planned, dto.Status);
        Assert.Empty(dto.Movements);
    }

    [Fact]
    public async Task GetTrainingSessions_WithNoSessions_ReturnsEmptyList()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);

        var response = await _client.GetAsync($"/api/training-session/get-all/{programId}");

        response.EnsureSuccessStatusCode();
        var sessions = await response.Content.ReadFromJsonAsync<List<TrainingSessionDTO>>();
        Assert.NotNull(sessions);
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetTrainingSession_WithValidId_ReturnsSession()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();

        var response = await _client.GetAsync($"/api/training-session/get/{created!.TrainingSessionID}");

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<TrainingSessionDTO>();
        Assert.NotNull(session);
        Assert.Equal(created.TrainingSessionID, session.TrainingSessionID);
    }

    [Fact]
    public async Task UpdateTrainingSession_WithValidData_ReturnsUpdatedSession()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();

        var updateRequest = new UpdateTrainingSessionRequest
        {
            TrainingSessionID = created!.TrainingSessionID,
            TrainingProgramID = programId,
            Date = createRequest.Date.AddDays(1),
            Status = TrainingSessionStatus.Completed
        };

        var response = await _client.PutAsJsonAsync("/api/training-session/update", updateRequest);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TrainingSessionDTO>();
        Assert.NotNull(updated);
        Assert.Equal(updateRequest.Date, updated.Date);
        Assert.Equal(updateRequest.Status, updated.Status);
    }

    [Fact]
    public async Task DeleteTrainingSession_WithValidId_ReturnsNoContent()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();

        var response = await _client.DeleteAsync($"/api/training-session/delete/{created!.TrainingSessionID}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deleted
        var getResponse = await _client.GetAsync($"/api/training-session/get/{created.TrainingSessionID}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    #endregion

    #region Validation Failure Cases

    [Fact]
    public async Task CreateTrainingSession_WithMissingDate_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);

        var request = new
        {
            TrainingProgramID = programId
            // Missing Date
        };

        var response = await _client.PostAsJsonAsync("/api/training-session/create", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrainingSession_WithMissingStatus_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();

        // Missing Status
        var updateRequest = new
        {
            TrainingSessionID = created!.TrainingSessionID,
            TrainingProgramID = programId,
            Date = createRequest.Date.AddDays(1)
        };

        var response = await _client.PutAsJsonAsync("/api/training-session/update", updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Not Found Cases

    [Fact]
    public async Task GetTrainingSession_WithNonExistentId_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.GetAsync($"/api/training-session/get/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrainingSession_WithNonExistentId_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);

        var updateRequest = new UpdateTrainingSessionRequest
        {
            TrainingSessionID = Guid.NewGuid(),
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now),
            Status = TrainingSessionStatus.Completed
        };

        var response = await _client.PutAsJsonAsync("/api/training-session/update", updateRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTrainingSession_WithNonExistentId_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.DeleteAsync($"/api/training-session/delete/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetTrainingSession_WithOtherUsersSession_ReturnsNotFound()
    {
        // Create as test user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();

        // Try to get as other user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var response = await _client.GetAsync($"/api/training-session/get/{created!.TrainingSessionID}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrainingSession_WithOtherUsersSession_ReturnsNotFound()
    {
        // Create as test user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();

        // Try to update as other user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var updateRequest = new UpdateTrainingSessionRequest
        {
            TrainingSessionID = created!.TrainingSessionID,
            TrainingProgramID = programId,
            Date = createRequest.Date.AddDays(1),
            Status = TrainingSessionStatus.Completed
        };

        var response = await _client.PutAsJsonAsync("/api/training-session/update", updateRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTrainingSession_WithOtherUsersSession_ReturnsNotFound()
    {
        // Create as test user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingSessionDTO>();

        // Try to delete as other user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var response = await _client.DeleteAsync($"/api/training-session/delete/{created!.TrainingSessionID}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/training-session/get-all/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/training-session/get/00000000-0000-0000-0000-000000000000")]
    public async Task UnauthenticatedRequests_ReturnUnauthorized(string endpoint)
    {
        var response = await _client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/training-session/delete/00000000-0000-0000-0000-000000000000")]
    public async Task UnauthenticatedDeleteRequests_ReturnUnauthorized(string endpoint)
    {
        var response = await _client.DeleteAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedCreateRequest_ReturnsUnauthorized()
    {
        var programId = Guid.NewGuid();
        var createRequest = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now)
        };

        var response = await _client.PostAsJsonAsync("/api/training-session/create", createRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUpdateRequest_ReturnsUnauthorized()
    {
        var updateRequest = new UpdateTrainingSessionRequest
        {
            TrainingSessionID = Guid.NewGuid(),
            TrainingProgramID = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.Now),
            Status = TrainingSessionStatus.Completed
        };

        var response = await _client.PutAsJsonAsync("/api/training-session/update", updateRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateTrainingSession_WithFutureDate_WorksCorrectly()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var request = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now.AddYears(1))
        };

        var response = await _client.PostAsJsonAsync("/api/training-session/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<TrainingSessionDTO>();
        Assert.Equal(request.Date, dto!.Date);
    }

    [Fact]
    public async Task CreateTrainingSession_WithPastDate_WorksCorrectly()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var programId = await CreateTrainingProgramForUser(_testUserId);
        var request = new CreateTrainingSessionRequest
        {
            TrainingProgramID = programId,
            Date = DateOnly.FromDateTime(DateTime.Now.AddYears(-1))
        };

        var response = await _client.PostAsJsonAsync("/api/training-session/create", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<TrainingSessionDTO>();
        Assert.Equal(request.Date, dto!.Date);
    }

    #endregion
}
