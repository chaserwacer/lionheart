using lionheart.Data;
using lionheart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;    // <-- this is the official OpenAIClient



var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// Only register SQLite if not running in Testing environment
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ModelContext>(options =>
        options.UseSqlite("Data Source=./Data/lionheart.db"));
}

builder.Services.AddOpenApi();

builder.Services.AddAuthorization();
services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<ModelContext>();

///////////////////MCP Server Setup////////////////////
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    // .WithPrompts<StringFormatPrompt>()
    // .WithPrompts<TemplateServerPrompt>()
    .WithToolsFromAssembly();
//builder.Services.AddSingleton<TemplateServerPrompt>();  //TODO: Validate this works [provides templates accessible via server??]
/////////////////////////////////////////////////////

builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<IActivityService, ActivityService>();
builder.Services.AddTransient<IOuraService, OuraService>();
builder.Services.AddTransient<IWellnessService, WellnessService>();
builder.Services.AddTransient<ITrainingProgramService, TrainingProgramService>();
builder.Services.AddTransient<ITrainingSessionService, TrainingSessionService>();
builder.Services.AddTransient<IMovementService, MovementService>();
builder.Services.AddTransient<ISetEntryService, SetEntryService>();
builder.Services.AddSingleton<OpenAiService>();
builder.Services.AddHttpClient<IOuraService, OuraService>(client =>
{
    client.BaseAddress = new Uri("https://api.ouraring.com/v2/usercollection");
});

string model = "gpt-4o";
string key = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is not configured.");


IChatClient client =
    new ChatClientBuilder(new OpenAIClient(key).GetChatClient(model ?? "gpt-4o").AsIChatClient())
    .UseFunctionInvocation()
    .Build();

builder.Services.AddSingleton<IChatClient>(client);

builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // <-- Add this
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information; // ✅ stdout clean, logs go to stderr
});



var app = builder.Build();

app.MapIdentityApi<IdentityUser>();
app.MapOpenApi();
app.MapScalarApiReference();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseDeveloperExceptionPage();

app.UseSwagger(); // <-- Add this
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lionheart API V1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

app.UseExceptionHandler(errorApp =>
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionFeature?.Error;

        // Optional: log it
        // logger.LogError(exception, "Unhandled exception");

        var errorResponse = new
        {
            Message = "An internal server error occurred.",
            TraceId = context.TraceIdentifier // helps with debugging
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    })
);

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();
// /app.UseStaticFiles();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

// app.MapFallbackToFile("about", "about.html");
// app.MapFallbackToFile("index.html");

// Only run TypeScript generation in non-test environments
if (!app.Environment.IsEnvironment("Testing"))
{
    #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    Task.Run(async () =>
        {
            Thread.Sleep(2000); // wait for the server to start
            await new TsClientGenerator().SimpleGenerate("http://localhost:7025/swagger/v1/swagger.json");
        });
    #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
}

app.MapMcp();

app.Run();

public partial class Program { }
[McpServerToolType]
public class MCPGUY
{
    [McpServerTool, Description("Reverse")]
    public static string Reverse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        char[] charArray = input.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }
}



