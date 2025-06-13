using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using System.Net;
using lionheart.Data;
using lionheart.Model.TrainingProgram;
using lionheart.Model.DTOs;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost; // Add this using

namespace lionheart.Tests.Integration.EndpointsTests;

/// <summary>
/// Integration tests for the Training Program endpoints.
/// Tests cover creation, retrieval, updating, and deletion of training programs,
/// as well as validation and authorization scenarios.
/// Tests are run against a shared in-memory database to ensure isolation and consistency.
/// </summary>
public class TrainingProgramEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testUserId = Guid.NewGuid().ToString();
    private readonly string _otherUserId = Guid.NewGuid().ToString();
    private readonly string _dbName = Guid.NewGuid().ToString(); // Shared DB name

    public TrainingProgramEndpointsTests(WebApplicationFactory<Program> factory)
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

    #region Success Cases

    [Fact]
    public async Task GetTrainingPrograms_WithNoPrograms_ReturnsEmptyList()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        // Act
        var response = await _client.GetAsync("/api/training-program/get-all");

        // Assert
        response.EnsureSuccessStatusCode();
        var programs = await response.Content.ReadFromJsonAsync<List<TrainingProgram>>();
        Assert.NotNull(programs);
        Assert.Empty(programs);
    }

    [Fact]
    public async Task CreateTrainingProgram_WithValidData_ReturnsCreatedProgram()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new CreateTrainingProgramRequest
        {
            Title = "Test Strength Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Strength", "Powerlifting" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/training-program/create", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdProgram = await response.Content.ReadFromJsonAsync<TrainingProgram>();

        Assert.NotNull(createdProgram);
        Assert.Equal(request.Title, createdProgram.Title);
        Assert.Equal(request.StartDate, createdProgram.StartDate);
        Assert.Equal(request.EndDate, createdProgram.EndDate);
        Assert.Equal(request.Tags, createdProgram.Tags);
        Assert.Equal(Guid.Parse(_testUserId), createdProgram.UserID);
        Assert.NotEqual(Guid.Empty, createdProgram.TrainingProgramID);
    }

    [Fact]
    public async Task GetTrainingProgram_WithValidId_ReturnsProgram()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Test Program for Retrieval",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Test" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        // Act
        var response = await _client.GetAsync($"/api/training-program/get/{createdProgram!.TrainingProgramID}");

        // Assert
        response.EnsureSuccessStatusCode();
        var retrievedProgram = await response.Content.ReadFromJsonAsync<TrainingProgram>();

        Assert.NotNull(retrievedProgram);
        Assert.Equal(createdProgram.TrainingProgramID, retrievedProgram.TrainingProgramID);
        Assert.Equal(createdProgram.Title, retrievedProgram.Title);
        Assert.Equal(createdProgram.StartDate, retrievedProgram.StartDate);
        Assert.Equal(createdProgram.EndDate, retrievedProgram.EndDate);
    }

    [Fact]
    public async Task UpdateTrainingProgram_WithValidData_ReturnsUpdatedProgram()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Original Title",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Original" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        var updateRequest = new UpdateTrainingProgramRequest
        {
            TrainingProgramID = createdProgram!.TrainingProgramID,
            Title = "Updated Title",
            StartDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(31)),
            Tags = new List<string> { "Updated", "Modified" }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/training-program/update", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var updatedProgram = await response.Content.ReadFromJsonAsync<TrainingProgram>();

        Assert.NotNull(updatedProgram);
        Assert.Equal(updateRequest.TrainingProgramID, updatedProgram.TrainingProgramID);
        Assert.Equal(updateRequest.Title, updatedProgram.Title);
        Assert.Equal(updateRequest.StartDate, updatedProgram.StartDate);
        Assert.Equal(updateRequest.EndDate, updatedProgram.EndDate);
        Assert.Equal(updateRequest.Tags, updatedProgram.Tags);
    }

    [Fact]
    public async Task DeleteTrainingProgram_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Program to Delete",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Delete" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);

        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        // Act
        var response = await _client.DeleteAsync($"/api/training-program/delete/{createdProgram!.TrainingProgramID}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify program is deleted
        var getResponse = await _client.GetAsync($"/api/training-program/get/{createdProgram.TrainingProgramID}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetTrainingPrograms_WithMultiplePrograms_ReturnsOnlyUserPrograms()
    {
        // Arrange - Create programs for both users
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var userProgram1 = new CreateTrainingProgramRequest
        {
            Title = "User 1 Program 1",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "User1" }
        };

        var userProgram2 = new CreateTrainingProgramRequest
        {
            Title = "User 1 Program 2",
            StartDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(31)),
            Tags = new List<string> { "User1" }
        };

        await _client.PostAsJsonAsync("/api/training-program/create", userProgram1);
        await _client.PostAsJsonAsync("/api/training-program/create", userProgram2);

        // Create program for other user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var otherUserProgram = new CreateTrainingProgramRequest
        {
            Title = "User 2 Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "User2" }
        };

        await _client.PostAsJsonAsync("/api/training-program/create", otherUserProgram);

        // Act - Get programs for first user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var response = await _client.GetAsync("/api/training-program/get-all");

        // Assert
        response.EnsureSuccessStatusCode();
        var programs = await response.Content.ReadFromJsonAsync<List<TrainingProgram>>();

        Assert.NotNull(programs);
        Assert.Equal(2, programs.Count);
        Assert.All(programs, p => Assert.Equal(Guid.Parse(_testUserId), p.UserID));
        Assert.Contains(programs, p => p.Title == "User 1 Program 1");
        Assert.Contains(programs, p => p.Title == "User 1 Program 2");
    }

    #endregion

    #region Validation Failure Cases

    [Fact]
    public async Task CreateTrainingProgram_WithEmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new CreateTrainingProgramRequest
        {
            Title = "", // Invalid - empty title
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/training-program/create", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTrainingProgram_WithEndDateBeforeStartDate_ReturnsBadRequest()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new CreateTrainingProgramRequest
        {
            Title = "Invalid Date Range Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), // Invalid - end before start
            Tags = new List<string> { "Invalid" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/training-program/create", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrainingProgram_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        // Create a valid program first
        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Valid Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Valid" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        var updateRequest = new UpdateTrainingProgramRequest
        {
            TrainingProgramID = createdProgram!.TrainingProgramID,
            Title = "", // Invalid - empty title
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-5)), // Invalid - end before start
            Tags = new List<string>()
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/training-program/update", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Not Found Cases

    [Fact]
    public async Task GetTrainingProgram_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/training-program/get/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrainingProgram_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var updateRequest = new UpdateTrainingProgramRequest
        {
            TrainingProgramID = Guid.NewGuid(), // Non-existent ID
            Title = "Updated Title",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Updated" }
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/training-program/update", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTrainingProgram_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/training-program/delete/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetTrainingProgram_WithOtherUsersProgram_ReturnsNotFound()
    {
        // Arrange - Create program as first user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Private Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Private" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        // Act - Try to access as different user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var response = await _client.GetAsync($"/api/training-program/get/{createdProgram!.TrainingProgramID}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrainingProgram_WithOtherUsersProgram_ReturnsNotFound()
    {
        // Arrange - Create program as first user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Protected Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Protected" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        // Act - Try to update as different user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var updateRequest = new UpdateTrainingProgramRequest
        {
            TrainingProgramID = createdProgram!.TrainingProgramID,
            Title = "Hacked Title",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Hacked" }
        };

        var response = await _client.PutAsJsonAsync("/api/training-program/update", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTrainingProgram_WithOtherUsersProgram_ReturnsNotFound()
    {
        // Arrange - Create program as first user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Secure Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Secure" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        // Act - Try to delete as different user
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var response = await _client.DeleteAsync($"/api/training-program/delete/{createdProgram!.TrainingProgramID}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/training-program/get-all")]
    [InlineData("/api/training-program/get/00000000-0000-0000-0000-000000000000")]
    public async Task UnauthenticatedRequests_ReturnUnauthorized(string endpoint)
    {
        // Arrange - Don't set authorization header

        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/training-program/delete/00000000-0000-0000-0000-000000000000")]
    public async Task UnauthenticatedDeleteRequests_ReturnUnauthorized(string endpoint)
    {
        // Arrange - Don't set authorization header

        // Act
        var response = await _client.DeleteAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedCreateRequest_ReturnsUnauthorized()
    {
        // Arrange - Don't set authorization header
        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Test",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUpdateRequest_ReturnsUnauthorized()
    {
        // Arrange - Don't set authorization header
        var updateRequest = new UpdateTrainingProgramRequest
        {
            TrainingProgramID = Guid.NewGuid(),
            Title = "Test",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string>()
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/training-program/update", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Fact]
    public async Task CreateTrainingProgram_WithSpecialCharactersInTitle_WorksCorrectly()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new CreateTrainingProgramRequest
        {
            Title = "Тест Program with émojis 🏋️‍♂️ & special chars!@#$%",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string> { "Unicode", "Special-Chars", "Test_Tag" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/training-program/create", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdProgram = await response.Content.ReadFromJsonAsync<TrainingProgram>();
        Assert.Equal(request.Title, createdProgram!.Title);
        Assert.Equal(request.Tags, createdProgram.Tags);
    }

    [Fact]
    public async Task CreateTrainingProgram_WithEmptyTagsList_WorksCorrectly()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var request = new CreateTrainingProgramRequest
        {
            Title = "Program with No Tags",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            Tags = new List<string>() // Empty tags list
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/training-program/create", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdProgram = await response.Content.ReadFromJsonAsync<TrainingProgram>();
        Assert.Empty(createdProgram!.Tags);
    }

    [Fact]
    public async Task UpdateTrainingProgram_PartialUpdate_MaintainsOtherProperties()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Original Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(60)),
            Tags = new List<string> { "Original", "Unchanged" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();

        var updateRequest = new UpdateTrainingProgramRequest
        {
            TrainingProgramID = createdProgram!.TrainingProgramID,
            Title = "Updated Title Only",
            StartDate = createdProgram.StartDate, // Keep same
            EndDate = createdProgram.EndDate, // Keep same
            Tags = createdProgram.Tags // Keep same
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/training-program/update", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var updatedProgram = await response.Content.ReadFromJsonAsync<TrainingProgram>();

        Assert.Equal("Updated Title Only", updatedProgram!.Title);
        Assert.Equal(createdProgram.StartDate, updatedProgram.StartDate);
        Assert.Equal(createdProgram.EndDate, updatedProgram.EndDate);
        Assert.Equal(createdProgram.Tags, updatedProgram.Tags);
        Assert.Equal(createdProgram.UserID, updatedProgram.UserID); // UserID should never change
    }

    #endregion

    #region Full Workflow Tests

    [Fact]
    public async Task EndpointFlow_CreateUpdateGetDelete_WorksCorrectly()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        // Act & Assert - Create
        var createRequest = new CreateTrainingProgramRequest
        {
            Title = "Integration Test Program",
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(60)),
            Tags = new List<string> { "Integration", "Test" }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/training-program/create", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdProgram = await createResponse.Content.ReadFromJsonAsync<TrainingProgram>();
        Assert.NotNull(createdProgram);

        // Act & Assert - Get specific
        var getResponse = await _client.GetAsync($"/api/training-program/get/{createdProgram.TrainingProgramID}");
        getResponse.EnsureSuccessStatusCode();
        var retrievedProgram = await getResponse.Content.ReadFromJsonAsync<TrainingProgram>();
        Assert.Equal(createdProgram.TrainingProgramID, retrievedProgram!.TrainingProgramID);

        // Act & Assert - Get all (should include our program)
        var getAllResponse = await _client.GetAsync("/api/training-program/get-all");
        getAllResponse.EnsureSuccessStatusCode();
        var allPrograms = await getAllResponse.Content.ReadFromJsonAsync<List<TrainingProgram>>();
        Assert.Contains(allPrograms!, p => p.TrainingProgramID == createdProgram.TrainingProgramID);

        // Act & Assert - Update
        var updateRequest = new UpdateTrainingProgramRequest
        {
            TrainingProgramID = createdProgram.TrainingProgramID,
            Title = "Updated Integration Test Program",
            StartDate = createdProgram.StartDate,
            EndDate = createdProgram.EndDate.AddDays(30),
            Tags = new List<string> { "Updated", "Integration", "Test" }
        };

        var updateResponse = await _client.PutAsJsonAsync("/api/training-program/update", updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updatedProgram = await updateResponse.Content.ReadFromJsonAsync<TrainingProgram>();
        Assert.Equal("Updated Integration Test Program", updatedProgram!.Title);

        // Act & Assert - Delete
        var deleteResponse = await _client.DeleteAsync($"/api/training-program/delete/{createdProgram.TrainingProgramID}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deletion
        var getAfterDeleteResponse = await _client.GetAsync($"/api/training-program/get/{createdProgram.TrainingProgramID}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDeleteResponse.StatusCode);

        // Verify not in get-all list
        var getAllAfterDeleteResponse = await _client.GetAsync("/api/training-program/get-all");
        getAllAfterDeleteResponse.EnsureSuccessStatusCode();
        var allProgramsAfterDelete = await getAllAfterDeleteResponse.Content.ReadFromJsonAsync<List<TrainingProgram>>();
        Assert.DoesNotContain(allProgramsAfterDelete!, p => p.TrainingProgramID == createdProgram.TrainingProgramID);
    }

    [Fact]
    public async Task ConcurrentUsersWorkflow_IsolatedCorrectly()
    {
        // Create programs for both users concurrently
        var user1Task = CreateProgramForUser(_testUserId, "User 1 Program");
        var user2Task = CreateProgramForUser(_otherUserId, "User 2 Program");

        await Task.WhenAll(user1Task, user2Task);

        var user1Program = await user1Task;
        var user2Program = await user2Task;

        // Verify each user can only see their own programs
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _testUserId);

        var user1Programs = await GetUserPrograms();
        Assert.Single(user1Programs);
        Assert.Equal(user1Program.TrainingProgramID, user1Programs[0].TrainingProgramID);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", _otherUserId);

        var user2Programs = await GetUserPrograms();
        Assert.Single(user2Programs);
        Assert.Equal(user2Program.TrainingProgramID, user2Programs[0].TrainingProgramID);
    }

    private async Task<TrainingProgram> CreateProgramForUser(string userId, string title)
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
        return (await response.Content.ReadFromJsonAsync<TrainingProgram>())!;
    }

    private async Task<List<TrainingProgram>> GetUserPrograms()
    {
        var response = await _client.GetAsync("/api/training-program/get-all");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<TrainingProgram>>())!;
    }

    #endregion
}

public class TestAuthenticationSchemeHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationSchemeHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorizationHeader = Request.Headers["authorization"].ToString();
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing authorization header"));
        }

        var userId = authorizationHeader.Replace("Test ", "");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
        };

        // Use Identity's default authentication type
        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}