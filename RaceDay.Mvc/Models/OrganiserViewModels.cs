using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models
{
    public class DashboardViewModel
    {
        public List<OrganiserEventSummaryDto> Events { get; set; } = new();
        public int UpcomingCount { get; set; }
        public int TotalEnrolments { get; set; }
        public DateTime? NextEventDate { get; set; }
        public string? Error { get; set; }
    }

    public class EventFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Please give the event a name.")]
        [StringLength(150, ErrorMessage = "The name can be at most 150 characters.")]
        public string Name { get; set; } = "";

        [StringLength(2000, ErrorMessage = "The description can be at most 2000 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please choose the event date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Event date")]
        public DateTime EventDate { get; set; } = DateTime.Today.AddDays(30);

        [Required(ErrorMessage = "Please enter where the event takes place.")]
        [StringLength(150, ErrorMessage = "The location can be at most 150 characters.")]
        public string Location { get; set; } = "";

        [Range(0.01, 9999.99, ErrorMessage = "The distance must be between 0.01 and 9999.99 km.")]
        [Display(Name = "Distance (km)")]
        public decimal DistanceKm { get; set; }

        [Required(ErrorMessage = "Please choose Run, Walk or Cycle.")]
        [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Please choose Run, Walk or Cycle.")]
        [Display(Name = "Event type")]
        public string EventType { get; set; } = "Run";

        // Shown as a preview when editing an event that already has a banner
        public string? BannerImageUrl { get; set; }

        // The new banner image, if one was chosen
        public IFormFile? BannerFile { get; set; }
    }

    // Shared by the Categories, Enrolments and Results pages: which of the organiser's events is on screen
    public class EventPickerViewModel
    {
        public List<OrganiserEventSummaryDto> Events { get; set; } = new();
        public int? SelectedEventId { get; set; }
    }

    public class CategoriesViewModel : EventPickerViewModel
    {
        public List<CategoryDto> Categories { get; set; } = new();
        public string? Error { get; set; }
    }

    public class EnrolmentsViewModel : EventPickerViewModel
    {
        public List<EnrolmentDto> Enrolments { get; set; } = new();
        public string? Error { get; set; }
    }

    public class ResultsViewModel : EventPickerViewModel
    {
        public OrganiserEventSummaryDto? SelectedEvent { get; set; }
        public List<EnrolmentDto> Awaiting { get; set; } = new();
        public List<ResultDto> Recorded { get; set; } = new();
        public bool CanRecord { get; set; }
        public string? Error { get; set; }
    }
}
