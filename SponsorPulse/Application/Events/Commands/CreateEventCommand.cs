using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace SponsorPulse.Application.Events.Commands;

public class CreateEventCommand : IValidatableObject
{
    private static readonly DateTime DefaultEventDate = TimeProvider
        .System.GetUtcNow()
        .ToLocalTime()
        .DateTime.Date;

    [Required(ErrorMessage = "Le nom de l'événement est obligatoire.")]
    [StringLength(100, ErrorMessage = "Le nom est trop long (100 caractères max).")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Une description est requise.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La date est obligatoire.")]
    public DateTime? Date { get; set; } = DefaultEventDate;

    [Required(ErrorMessage = "L'heure de démarrage est obligatoire.")]
    public TimeOnly? StartedAtTime { get; set; } =
        TimeOnly.FromDateTime(TimeProvider.System.GetUtcNow().ToLocalTime().DateTime);

    [Required(ErrorMessage = "Veuillez sélectionner une plateforme.")]
    public string StreamPlatform { get; set; } = "Twitch";

    [Required(ErrorMessage = "Le broadcaster ID Twitch est requis.")]
    public string BroadcasterId { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? SocialStartDate { get; set; } = DefaultEventDate.AddDays(-2);

    [DataType(DataType.Date)]
    public DateTime? SocialEndDate { get; set; } = DefaultEventDate.AddDays(2);

    [StringLength(500, ErrorMessage = "Les hashtags ne peuvent pas dépasser 500 caractères.")]
    public string? TwitterHashtags { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var localToday = TimeProvider.System.GetUtcNow().ToLocalTime().Date;

        if (Date.HasValue && Date.Value.Date < localToday)
        {
            yield return new ValidationResult(
                "La date de l'événement ne peut pas être antérieure à aujourd'hui.",
                new[] { nameof(Date) }
            );
        }

        if (!StartedAtTime.HasValue)
        {
            yield return new ValidationResult(
                "L'heure de démarrage est obligatoire.",
                new[] { nameof(StartedAtTime) }
            );
        }

        if (SocialStartDate.HasValue && SocialEndDate.HasValue)
        {
            if (SocialStartDate.Value.Date > SocialEndDate.Value.Date)
            {
                yield return new ValidationResult(
                    "La date de début des réseaux sociaux doit être antérieure ou égale à la date de fin.",
                    new[] { nameof(SocialStartDate), nameof(SocialEndDate) }
                );
            }
        }
    }
}
