using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using lionheart.Model.TrainingProgram;


public interface ISetEntry
{
   public Guid MovementID { get; init; }
   public Movement Movement { get; set; }
   public Guid SetEntryID { get; init; }
   public double ActualRPE { get; set; }
}