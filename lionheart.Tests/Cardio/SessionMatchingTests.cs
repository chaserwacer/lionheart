using lionheart.Model.Analysis;
using lionheart.Services.Cardio;
using lionheart.Tests.Support;

namespace lionheart.Tests.Cardio;

/// <summary>How much of a workout lies inside a logged session, including a workout of no length.</summary>
public class SessionMatchingTests
{
    private static readonly DateTimeOffset Start = new(2025, 4, 2, 17, 0, 0, TimeSpan.Zero);

    private static readonly Interval Logged = new(Start, Start.AddMinutes(60));

    [Theory]
    [InlineData(0, 30, 1.0)]
    [InlineData(45, 30, 0.5)]
    [InlineData(50, 30, 1.0 / 3)]
    [InlineData(70, 30, 0.0)]
    public void The_share_inside_is_the_part_of_the_workouts_own_length_the_session_covers(int startsAfter, int lasts, double share)
    {
        var workout = (Start.AddMinutes(startsAfter), Start.AddMinutes(startsAfter + lasts));

        Assert.Equal(share, SessionMatching.ShareInside(workout, Logged), precision: 9);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(60, 1.0)]
    [InlineData(61, 0.0)]
    public void A_workout_of_no_length_lies_wholly_inside_a_session_when_its_moment_does_ends_included(int at, double share)
    {
        var moment = Start.AddMinutes(at);

        Assert.Equal(share, SessionMatching.ShareInside((moment, moment), Logged));
    }

    [Fact]
    public void A_workout_of_no_length_is_one_held_at_the_same_moment()
    {
        var held = Some.CompletedSession(Guid.NewGuid()) with { Period = new Interval(Start, Start), Sets = [Some.TrailRunSet] };

        Assert.Equal(new SessionMatch.LoggedRun(held.TrainingSessionId), SessionMatching.Find((Start, Start), [held]));
    }
}
