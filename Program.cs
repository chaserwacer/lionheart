using lionheart.Data;
using lionheart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;


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

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();


builder.Services.AddMemoryCache();
builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<IActivityService, ActivityService>();
builder.Services.AddTransient<IOuraService, OuraService>();
builder.Services.AddTransient<IWellnessService, WellnessService>();
builder.Services.AddTransient<ITrainingProgramService, TrainingProgramService>();
builder.Services.AddTransient<ITrainingSessionService, TrainingSessionService>();
builder.Services.AddTransient<IMovementService, MovementService>();
builder.Services.AddScoped<IInjuryService, InjuryService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ILiftSetEntryService, LiftSetEntryService>();
builder.Services.AddTransient<IDTSetEntryService, DTSetEntryService>();


builder.Services.AddHttpClient<IOuraService, OuraService>(client =>
{
    client.BaseAddress = new Uri("https://api.ouraring.com/v2/usercollection");
});



builder.Services
  .AddControllers()
  .AddJsonOptions(opts =>
  {
      // if you're on .NET 7+, this is built-in,
      // otherwise pull in a custom converter like below.
      opts.JsonSerializerOptions.Converters.Add(
        new DateOnlyJsonConverter("yyyy-MM-dd"));
  });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });

builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // <-- Add this
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information;
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

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

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





