using lionheart.Data;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Movement;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Changes;

/// <summary>The names of the athlete's movements a change or a plan mentions, read once.</summary>
public sealed record MovementNameBook(IReadOnlyDictionary<Guid, string> Names)
{
    public static readonly MovementNameBook Empty = new(new Dictionary<Guid, string>());

    public string NameOf(Guid movementDataId) => Names.GetValueOrDefault(movementDataId, "an unlisted movement");

    /// <summary>Whether the movement is one of his: the book holds only his.</summary>
    public bool Knows(Guid movementDataId) => Names.ContainsKey(movementDataId);

    /// <summary>The movements a plan prescribes, which a freeform line names none of.</summary>
    public static IReadOnlyList<Guid> MovementsOf(IEnumerable<SetPrescription> prescriptions) =>
        [.. prescriptions.SelectMany(prescription => prescription switch
        {
            LiftPrescription lift => [lift.MovementDataId],
            DistanceTimePrescription distance => [distance.MovementDataId],
            FreeformPrescription => Array.Empty<Guid>(),
            _ => throw new System.Diagnostics.UnreachableException(),
        })];
}

internal static class MovementNameBooks
{
    extension(ModelContext context)
    {
        /// <summary>The names of those of these movements that are his; another athlete's is left out.</summary>
        public async Task<MovementNameBook> MovementNamesAsync(Guid userId, IEnumerable<Guid> ids, CancellationToken ct)
        {
            var wanted = ids.Distinct().ToList();
            if (wanted.Count == 0) return MovementNameBook.Empty;

            return new MovementNameBook(await context.MovementDatas
                .AsNoTracking()
                .Where(movement => movement.UserID == userId && wanted.Contains(movement.MovementDataID))
                .Select(movement => new
                {
                    movement.MovementDataID,
                    Base = movement.MovementBase.Name,
                    Equipment = movement.Equipment.Name,
                    Modifier = movement.MovementModifier == null ? null : movement.MovementModifier.Name,
                })
                .ToDictionaryAsync(
                    movement => movement.MovementDataID,
                    movement => MovementNames.Of(movement.Base, movement.Equipment, movement.Modifier),
                    ct));
        }
    }
}
