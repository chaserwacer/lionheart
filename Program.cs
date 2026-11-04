using lionheart.Data;
using lionheart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using lionheart.Functional;
using Model.Chat.Tools;
using lionheart.Services.Chat;
using Services.Chat;
using Model.Tools;
using lionheart.Services.Training;
using lionheart.Services.Profile;
using lionheart.Services.Embedding;
using lionheart.Services.Llm;
using lionheart.Services.Interpretation;
using lionheart.Services.Contexts;
using lionheart.Services.Training.Focus;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Score;
using lionheart.Services.Training.Programs;
using lionheart.Services.Training.Routines;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Services.Training.Sessions;
using lionheart.Endpoints.OpenApi;
using lionheart.Services.Cardio;
using lionheart.Services.Habits;
using lionheart.Services.Oura;
using lionheart.Services.Oura.Import;
using lionheart.Services.Strong;
using lionheart.Services.CoachedSheets;
using lionheart.Services.Chat.Memory;
using lionheart.Services.Chat.Thread;
using lionheart.Services.BodyComposition;
using lionheart.Services.Health.Import;
using lionheart.Services.Health.Measurements;
using lionheart.Services.Health.Runs;

// Load .env into the process environment before the host reads configuration, so the Llm__,
// Anthropic__, LmStudio__ and Embedding__ keys flow through the default environment-variable
// provider (which maps "__" to ":").
DotNetEnv.Env.TraversePath().Load();

// A one-off command runs instead of the server, and before it is built: the web host refuses to
// start without LM Studio configured, which an import has no use for.
if (args is [OuraImportCommand.Verb, ..])
{
    return await OuraImportCommand.RunAsync(args, CancellationToken.None);
}

if (args is [StrongImportCommand.Verb, ..])
{
    return await StrongImportCommand.RunAsync(args, CancellationToken.None);
}

if (args is [CoachedSheetImportCommand.Verb, ..])
{
    return await CoachedSheetImportCommand.RunAsync(args, CancellationToken.None);
}

if (args is [RebuildRecordsCommand.Verb, ..])
{
    return await RebuildRecordsCommand.RunAsync(args, CancellationToken.None);
}

if (args is [StrongUpgradeCommand.Verb, ..])
{
    return await StrongUpgradeCommand.RunAsync(args, CancellationToken.None);
}

if (args is [ScoreCalibrationCommand.Verb, ..])
{
    return await ScoreCalibrationCommand.RunAsync(args, CancellationToken.None);
}

if (args is [AddScanCommand.Verb, ..])
{
    return await AddScanCommand.RunAsync(args, CancellationToken.None);
}

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

// Only register SQLite if not running in Testing environment
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.AddLionheartDatabase();
}

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<ModelContext>();


// Which model serves chat, and which server serves embeddings — read and validated here so a
// missing or contradictory key stops the host naming the key, rather than failing the first
// request that needs a model. The two are independent: Anthropic has no embeddings endpoint, so
// "chat on Anthropic, embeddings on a local OpenAI-compatible server" is the expected production
// shape, not a fallback.
var llm = LlmConfiguration.Read(configuration).Match(
    valid => valid,
    problem => throw new InvalidOperationException(
        $"Language-model configuration is incomplete. {problem.Description}"));

builder.Services.AddSingleton(llm);

builder.Services.AddSingleton<IChatModel>(services =>
    ChatModelFactory.Create(llm.Chat, llm.Chat.Model, services.GetRequiredService<ILoggerFactory>()));

// A cheaper model for Athlete Context Card narratives, off the hot path. Wrapped so it and the
// flagship chat model can be two singletons of the same type without DI ambiguity.
builder.Services.AddSingleton(services => new NarrativeChatClient(
    ChatModelFactory.Create(llm.Chat, llm.Chat.NarrativeModel, services.GetRequiredService<ILoggerFactory>())));

// Embeddings always come from an OpenAI-compatible server. The model is pinned in configuration
// because vectors from different models are incomparable: changing it invalidates every stored
// vector and requires a full re-decoration pass, never a partial one.
builder.Services.AddSingleton(_ => ChatModelFactory.CreateEmbeddingClient(llm.Embedding));

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter("yyyy-MM-dd"));

        // System.Text.Json requires "$type" to be the FIRST property when reading a polymorphic
        // payload. The interpretation resume path echoes a parsed draft back to us, and any client
        // that reorders keys on the way would otherwise fail to deserialize for no visible reason.
        options.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true;
    });

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<IWellnessService, WellnessService>();
builder.Services.AddTransient<IMovementDataService, MovementDataService>();
builder.Services.AddTransient<IMovementBaseService, MovementBaseService>();
builder.Services.AddTransient<IEquipmentService, EquipmentService>();
builder.Services.AddTransient<IMovementModifierService, MovementModifierService>();
builder.Services.AddScoped<IInjuryService, InjuryService>();
builder.Services.AddTransient<IPersonalRecordService, PersonalRecordService>();
builder.Services.AddTransient<IPersonalRecordDetector, PersonalRecordDetector>();
builder.Services.AddTransient<PersonalRecordRebuild>();
builder.Services.AddTransient<IChatMessageService, ChatMessageService>();
builder.Services.AddTransient<IChatCompletionService, ChatCompletionService>();
builder.Services.AddTransient<IChatConversationService, ChatConversationService>();
builder.Services.AddTransient<ChatSystemPrompt>();
builder.Services.AddTransient<IChatThreadService, ChatThreadService>();
builder.Services.AddTransient<ISessionConsolidator, SessionConsolidator>();
builder.Services.AddTransient<IChatMemoryService, ChatMemoryService>();
builder.Services.AddTransient<IMemoryRecallService, MemoryRecallService>();
builder.Services.AddTransient<MemoryVectors>();
builder.Services.AddSingleton<ChatThreadGate>();
builder.Services.AddTransient<IAthleteContextCardService, AthleteContextCardService>();
builder.Services.AddTransient<IEmbeddingGenerator, OpenAiCompatibleEmbeddingGenerator>();
builder.Services.AddTransient<IInterpretationParser, InterpretationParser>();
builder.Services.AddTransient<IFacetResolver, FacetResolver>();
builder.Services.AddTransient<IMovementResolver, MovementResolver>();
builder.Services.AddTransient<IInterpretationResolver, InterpretationResolver>();
builder.Services.AddTransient<IInterpretationCommitter, InterpretationCommitter>();
builder.Services.AddTransient<IInterpretationEngine, InterpretationEngine>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<ITrainingSessionService, TrainingSessionService>();
builder.Services.AddTransient<IHealthWorkoutImportService, HealthWorkoutImportService>();
builder.Services.AddTransient<IRecordedRunService, RecordedRunService>();
builder.Services.AddTransient<IHealthMeasureService, HealthMeasureService>();
builder.Services.AddTransient<IHealthBodyweightImportService, HealthBodyweightImportService>();
builder.Services.AddTransient<IHealthBirthDateService, HealthBirthDateService>();
builder.Services.AddTransient<ITrainingProgramService, TrainingProgramService>();
builder.Services.AddTransient<IFocusPinService, FocusPinService>();
builder.Services.AddSingleton(_ => MovementPatternMap.Shipped);
builder.Services.AddTransient<IMovementClassificationService, MovementClassificationService>();
builder.Services.AddTransient<ITrainingVolumeService, TrainingVolumeService>();
builder.Services.AddTransient<IPriorityReportService, PriorityReportService>();
builder.Services.AddTransient<ITrainingScoreService, TrainingScoreService>();
builder.Services.AddTransient<IContextService, ContextService>();
builder.Services.AddTransient<IRoutineService, RoutineService>();
builder.Services.AddTransient<IPreviousPerformanceService, PreviousPerformanceService>();
builder.Services.AddTransient<IBodyweightService, BodyweightService>();
builder.Services.AddTransient<IBodyCompositionService, BodyCompositionService>();


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


builder.Services.AddTransient<IOuraReadService, OuraReadService>();
builder.Services.AddTransient<ICardioReportService, CardioReportService>();
builder.Services.AddTransient<IHabitsReportService, HabitsReportService>();

// The sync service reaches Oura over this client; OuraClient spells the absolute collection URLs,
// so no BaseAddress is set here.
builder.Services.AddHttpClient<IOuraSyncService, OuraSyncService>();


builder.Services.AddHttpClient();

// One OpenAPI document, from Swashbuckle. It is not only what the Scalar UI renders: the NSwag
// TypeScript generator below reads it too, and NSwag reads OpenAPI 3.0, which is the version
// Swashbuckle emits. AddOpenApi() describes the same endpoints but emits 3.1, so adding it back
// would mean a second document and a generator that has to cope with a version NSwag does not.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.DescribePolymorphicTypes());

// Distils ended chat-thread sessions into memory between messages. Not under test, where nothing
// should call a model on a timer.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<SessionConsolidationService>();
}

// The Svelte app imports a generated API client, so it has to be regenerated whenever the
// endpoints change. Development only — it writes into the source tree.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<TypeScriptApiClientGenerator>();
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information;
});



var app = builder.Build();

app.MapIdentityApi<IdentityUser>();

// Serves the document at /swagger/v1/swagger.json; Scalar renders it at /scalar.
app.UseSwagger();
app.MapScalarApiReference(options => options
    .WithTitle("Lionheart API")
    .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json"));

if (app.Environment.IsDevelopment())
{
    // Renders unhandled exceptions with the stack trace and the request. Development only:
    // in any other environment the handler below answers with an opaque 500 instead.
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

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

app.UseStaticFiles();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

// SPA deep links. adapter-static emits one index.html shell, so a non-API path that matched no
// endpoint has to serve it or a refresh on /training/session/{id} 404s. Unmatched /api paths must
// still 404 honestly — returning the shell there would hand the client HTML to parse as JSON.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var shell = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (!File.Exists(shell))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(shell);
});

app.Run();

return 0;

/// <summary>
/// Regenerates ClientApp's TypeScript API client from this server's own OpenAPI document, once
/// per run. The document is read over HTTP, so there is nothing to read until Kestrel is
/// listening: <see cref="StartedAsync"/> is the first moment at which that is true, which is
/// why this is an <see cref="IHostedLifecycleService"/> and not an ordinary hosted service.
/// </summary>
internal sealed class TypeScriptApiClientGenerator(
    IServer server,
    ILogger<TypeScriptApiClientGenerator> logger) : IHostedLifecycleService
{
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromSeconds(30);

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        if (ServerOrigin() is not { } origin)
        {
            logger.LogWarning("Skipped TypeScript client generation: the server reported no address to read the API document from.");
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GenerationTimeout);

        try
        {
            await new TsClientGenerator()
                .SimpleGenerate($"{origin}/swagger/v1/swagger.json")
                .WaitAsync(timeout.Token);

            logger.LogInformation("Regenerated ClientApp/src/lib/api/ApiClient.ts from {Origin}.", origin);
        }
        catch (Exception exception)
        {
            // A stale TypeScript client breaks the frontend build, not the API. Say so and serve.
            logger.LogWarning(exception, "TypeScript client generation failed; ApiClient.ts is unchanged.");
        }
    }

    /// <summary>
    /// The address to read the document from, preferring plain HTTP: NSwag fetches with its own
    /// HttpClient, which does not trust the local development certificate.
    /// </summary>
    private string? ServerOrigin()
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        var address = addresses.FirstOrDefault(a => a.StartsWith("http://")) ?? addresses.FirstOrDefault();

        return address?.TrimEnd('/').Replace("[::]", "localhost").Replace("0.0.0.0", "localhost");
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}


