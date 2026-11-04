using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// The mechanism every lifecycle transition rests on.
/// </summary>
/// <remarks>
/// A session's state is its TPH subtype, and EF cannot change a tracked entity's type in place —
/// so a transition is a remove and an add on one key. That only works because EF orders the DELETE
/// ahead of the INSERT inside a single SaveChanges; if a future EF version stopped doing so, every
/// transition route would break at once and this is the test that would say why.
/// </remarks>
public class LifecycleTransitionTests : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_row_moves_across_the_discriminator_under_one_key_in_a_single_save()
    {
        var planned = Some.PlannedSession(_database.UserId);
        using (var seed = _database.CreateContext())
        {
            seed.Sessions.Add(planned);
            await seed.SaveChangesAsync();
        }

        using (var transition = _database.CreateContext())
        {
            var tracked = await transition.Sessions.SingleAsync();
            transition.Sessions.Remove(tracked);
            transition.Sessions.Add(new ActiveTrainingSession(Some.Clock, [])
            {
                TrainingSessionId = planned.TrainingSessionId,
                UserId = planned.UserId,
                Notes = planned.Notes,
                Provenance = planned.Provenance,
                Membership = planned.Membership,
                Origin = planned.Origin,
                Source = planned.Source,
                CreatedAt = planned.CreatedAt,
            });

            await transition.SaveChangesAsync();
        }

        using var verify = _database.CreateContext();
        var after = Assert.IsType<ActiveTrainingSession>(await verify.Sessions.SingleAsync());
        Assert.Equal(planned.TrainingSessionId, after.TrainingSessionId);
        Assert.Equal(planned.CreatedAt, after.CreatedAt);
    }
}
