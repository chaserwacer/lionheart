using lionheart.Model.Training.Programs;
using lionheart.Tests.Support;

namespace lionheart.Tests.Persistence;

public sealed class ProgramPersistenceTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private TrainingProgram SaveAndRehydrate(TrainingProgram program)
    {
        using (var context = _db.CreateContext())
        {
            context.Programs.Add(program);
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        return fresh.Programs.Single(p => p.TrainingProgramId == program.TrainingProgramId);
    }

    [Fact]
    public void Planned_program_round_trips()
    {
        var program = Some.PlannedProgram(_db.UserId);

        var loaded = Assert.IsType<PlannedTrainingProgram>(SaveAndRehydrate(program));

        Assert.Equal(program.Title, loaded.Title);
        Assert.Equal(program.StartDate, loaded.StartDate);
        Assert.Equal(program.EndDate, loaded.EndDate);
        Assert.Equal(program.Tags, loaded.Tags);
    }

    [Fact]
    public void Active_program_round_trips()
    {
        var program = Some.ActiveProgram(_db.UserId);

        var loaded = Assert.IsType<ActiveTrainingProgram>(SaveAndRehydrate(program));

        Assert.Equal(program.Title, loaded.Title);
    }

    [Fact]
    public void Completed_program_round_trips_with_its_summary()
    {
        var program = Some.CompletedProgram(_db.UserId);

        var loaded = Assert.IsType<CompletedTrainingProgram>(SaveAndRehydrate(program));

        Assert.Equal(program.Summary, loaded.Summary);
    }

    [Fact]
    public void All_lifecycle_states_coexist_and_rehydrate_as_their_own_types()
    {
        using (var context = _db.CreateContext())
        {
            context.Programs.AddRange(
                Some.PlannedProgram(_db.UserId),
                Some.ActiveProgram(_db.UserId),
                Some.CompletedProgram(_db.UserId));
            context.SaveChanges();
        }

        using var fresh = _db.CreateContext();
        var loaded = fresh.Programs.ToList();

        Assert.Equal(3, loaded.Count);
        Assert.Single(loaded.OfType<PlannedTrainingProgram>());
        Assert.Single(loaded.OfType<ActiveTrainingProgram>());
        Assert.Single(loaded.OfType<CompletedTrainingProgram>());
    }
}
