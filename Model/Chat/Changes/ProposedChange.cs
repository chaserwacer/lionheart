using System.Text.Json.Serialization;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Sessions;

namespace lionheart.Model.Chat.Changes;

/// <summary>
/// A write the chat model has put to the athlete, held exactly as the page that makes the same
/// write would send it, so confirming it runs that page's service with that page's validation.
/// </summary>
/// <remarks>
/// One case per write the chat can propose rather than a generic "call this service" shape: the
/// card the athlete confirms is rendered from the case, so what he reads and what runs are the same
/// value, and nothing the model writes in prose can widen what a confirmation does. Each case carries
/// the request its service takes; a change to a planned session names the session as well, because
/// those routes take it apart from the body.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PlanSession), "planSession")]
[JsonDerivedType(typeof(RevisePlan), "revisePlan")]
[JsonDerivedType(typeof(RemovePlan), "removePlan")]
[JsonDerivedType(typeof(MarkNonTrainingDay), "markNonTrainingDay")]
[JsonDerivedType(typeof(FreeNonTrainingDay), "freeNonTrainingDay")]
[JsonDerivedType(typeof(PinPriorityLift), "pinPriorityLift")]
[JsonDerivedType(typeof(LogInjury), "logInjury")]
[JsonDerivedType(typeof(LogInjuryCheckIn), "logInjuryCheckIn")]
public abstract record ProposedChange
{
    private protected ProposedChange() { }

    /// <summary>Which part of the app the change lands in: what the client refreshes once it is written.</summary>
    [JsonIgnore]
    public abstract ChangeArea Area { get; }

    public sealed record PlanSession(PlanSessionRequest Plan) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Plan;
    }

    public sealed record RevisePlan(Guid TrainingSessionId, RevisePlanRequest Plan) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Plan;
    }

    public sealed record RemovePlan(Guid TrainingSessionId) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Plan;
    }

    public sealed record MarkNonTrainingDay(MarkNonTrainingDayRequest Request) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Availability;
    }

    public sealed record FreeNonTrainingDay(Guid NonTrainingDayId) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Availability;
    }

    public sealed record PinPriorityLift(PinFocusRequest Request) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Focus;
    }

    public sealed record LogInjury(CreateInjuryRequest Request) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Injuries;
    }

    public sealed record LogInjuryCheckIn(CreateInjuryEventRequest Request) : ProposedChange
    {
        public override ChangeArea Area => ChangeArea.Injuries;
    }
}

/// <summary>The part of the app a confirmed change lands in, and so which screens read again.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChangeArea>))]
public enum ChangeArea
{
    /// <summary>Planned sessions: Train, Today, availability and the week's targets.</summary>
    [JsonStringEnumMemberName("plan")] Plan,

    /// <summary>Non-training days: availability and every target prorated to it.</summary>
    [JsonStringEnumMemberName("availability")] Availability,

    /// <summary>Focus pins: the priorities and their aims.</summary>
    [JsonStringEnumMemberName("focus")] Focus,

    /// <summary>Injuries and their check-ins: Body, and what Today warns about going in.</summary>
    [JsonStringEnumMemberName("injuries")] Injuries,
}
