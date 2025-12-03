using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.TrainingProgram;
namespace lionheart.Model.TrainingProgram.SetEntry;


public interface ISetEntry
{
   // public Guid MovementID { get; init; }
   // public Movement Movement { get; set; }
   // public Guid SetEntryID { get; init; }
   // public double ActualRPE { get; set; }
   
}

public interface ISetEntryDTO
{
   // public Guid MovementID { get; init; }
   // public Guid SetEntryID { get; init; }
   // public double ActualRPE { get; init; }
}

public interface ICreateSetEntryRequest
{
   // public Guid MovementID { get; init; }
}

public interface IUpdateSetEntryRequest
{
   // public Guid SetEntryID { get; init; }
}
