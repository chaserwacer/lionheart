using lionheart.Data;
using lionheart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using OpenAI.Chat;
using Model.Chat.Tools;
using lionheart.Services.Chat;
using Services.Chat;
using Model.Tools;
using lionheart.Services.Training;


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


var openAiApiKey = configuration["OpenAI:ApiKey"];
builder.Services.AddSingleton(provider =>
    new ChatClient(model: "gpt-5.2", apiKey: openAiApiKey)
);

builder.Services
  .AddControllers()
  .AddJsonOptions(opts =>
  { 

      opts.JsonSerializerOptions.Converters.Add(
        new DateOnlyJsonConverter("yyyy-MM-dd"));
  });

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<IActivityService, ActivityService>();
builder.Services.AddTransient<IOuraService, OuraService>();
builder.Services.AddTransient<IWellnessService, WellnessService>();
builder.Services.AddTransient<ITrainingProgramService, TrainingProgramService>();
builder.Services.AddTransient<ITrainingSessionService, TrainingSessionService>();
builder.Services.AddTransient<IMovementService, MovementService>();
builder.Services.AddTransient<IMovementDataService, MovementDataService>();
builder.Services.AddTransient<IMovementBaseService, MovementBaseService>();
builder.Services.AddTransient<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IInjuryService, InjuryService>();
builder.Services.AddTransient<ILiftSetEntryService, LiftSetEntryService>();
builder.Services.AddTransient<IDTSetEntryService, DTSetEntryService>();
builder.Services.AddTransient<IPersonalRecordService, PersonalRecordService>();
builder.Services.AddTransient<IChatMessageService, ChatMessageService>();
builder.Services.AddTransient<ChatCompletionService, ChatCompletionService>();
builder.Services.AddTransient<IChatConversationService, ChatConversationService>();


builder.Services.AddSingleton<ToolRegistry>(sp =>
{
    return ToolRegistryBuilder.Build(builder.Services).GetAwaiter().GetResult();
});

builder.Services.AddSingleton(sp =>
{
    var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    options.Converters.Add(new DateOnlyJsonConverter("yyyy-MM-dd"));
    return options;
});

builder.Services.AddSingleton<ChatToolCallExecutor>();


builder.Services.AddHttpClient<IOuraService, OuraService>(client =>
{
    client.BaseAddress = new Uri("https://api.ouraring.com/v2/usercollection");
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
#pragma warning disable CS4014 
    Task.Run(async () =>
        {
            Thread.Sleep(2000); // wait for the server to start
            await new TsClientGenerator().SimpleGenerate("http://localhost:7025/swagger/v1/swagger.json");
        });
#pragma warning restore CS4014 
}

app.Run();





