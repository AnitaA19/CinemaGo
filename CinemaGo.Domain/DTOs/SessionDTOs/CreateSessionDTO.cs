using System.ComponentModel.DataAnnotations;

namespace CinemaGo.Domain.DTOs.SessionDTOs;

public class CreateSessionDTO : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int MovieId { get; set; }

    [Range(1, int.MaxValue)]
    public int HallId { get; set; }

    /// <summary>Start time in UTC (e.g. 2030-01-01T18:00:00Z). Must be in the future.</summary>
    [Required]
    public DateTime StartsAt { get; set; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal StandardPrice { get; set; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal VipPrice { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(StandardPrice, 2) != StandardPrice)
        {
            yield return new ValidationResult(
                "StandardPrice can have at most 2 decimal places.", new[] { nameof(StandardPrice) });
        }

        if (decimal.Round(VipPrice, 2) != VipPrice)
        {
            yield return new ValidationResult(
                "VipPrice can have at most 2 decimal places.", new[] { nameof(VipPrice) });
        }

        if (VipPrice < StandardPrice)
        {
            yield return new ValidationResult(
                "VipPrice cannot be lower than StandardPrice.", new[] { nameof(VipPrice) });
        }
    }}
