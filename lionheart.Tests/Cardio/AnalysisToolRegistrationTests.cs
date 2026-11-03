using lionheart.Services.Cardio;
using lionheart.Services.Habits;
using lionheart.Services.Training.Score;
using Microsoft.Extensions.DependencyInjection;
using Model.Chat.Tools;

namespace lionheart.Tests.Cardio;

/// <summary>
/// That the cardio and habits reports and the training score are reachable by the chat model.
/// </summary>
/// <remarks>
/// The registry skips a tool whose signature its mapper cannot describe without a word, so the
/// only symptom of a broken one would be an analyst that never asks about cardio.
/// </remarks>
public class AnalysisToolRegistrationTests
{
    [Theory]
    [InlineData("GetCardioReport", typeof(CardioReportService), typeof(ICardioReportService))]
    [InlineData("GetHabitsReport", typeof(HabitsReportService), typeof(IHabitsReportService))]
    [InlineData("GetTrainingScore", typeof(TrainingScoreService), typeof(ITrainingScoreService))]
    public async Task Each_report_is_a_tool_scoped_to_the_signed_in_athlete(string name, Type service, Type contract)
    {
        var services = new ServiceCollection();
        services.AddTransient<ICardioReportService, CardioReportService>();
        services.AddTransient<IHabitsReportService, HabitsReportService>();
        services.AddTransient<ITrainingScoreService, TrainingScoreService>();

        var registry = await ToolRegistryBuilder.Build(services);

        Assert.True(registry.TryGet(name, out var descriptor));
        Assert.NotNull(descriptor);
        Assert.True(descriptor.RequireAuthorization);
        Assert.Equal(service, descriptor.ServiceType);
        Assert.Equal(contract, Assert.Single(service.GetInterfaces()));
    }
}
