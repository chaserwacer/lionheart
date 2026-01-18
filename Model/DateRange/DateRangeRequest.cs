using System.ComponentModel.DataAnnotations;
namespace lionheart.Model.Request
{
    /// <summary>
    /// Date Range Request DTO, with validation for start and end dates.
    /// </summary>
    public record DateRangeRequest : IValidatableObject
    {
        [Required]
        public required DateTime StartDate { get; init; }

        [Required]
        public required DateTime EndDate { get; init; }

        /// <summary>
        /// Validates that the StartDate is not after the EndDate.
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate > EndDate)
            {
                yield return new ValidationResult(
                    "StartDate must be the same as, or come before, EndDate.",
                    new[] { nameof(StartDate), nameof(EndDate) }
                );
            }
        }
    }
}