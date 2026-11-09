using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models
{
    public class EventsIndexViewModel
    {
        public List<EventDto> Events { get; set; } = new();
        public string? Search { get; set; }
        public string? Type { get; set; }
        public decimal? MinDistance { get; set; }
        public decimal? MaxDistance { get; set; }
        public string? Error { get; set; }
    }

    public class EventDetailsViewModel
    {
        public EventDto Event { get; set; } = null!;
        public List<CategoryDto> Categories { get; set; } = new();
        public EnrolmentDto? MyEnrolment { get; set; }
        public bool IsParticipant { get; set; }
        public bool IsPast => Event.EventDate.Date < DateTime.Today;
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Please tell us your name.")]
        [StringLength(100, ErrorMessage = "Your name can be at most 100 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
        [StringLength(150, ErrorMessage = "Your email can be at most 150 characters.")]
        public string Email { get; set; } = "";

        [StringLength(20, ErrorMessage = "The phone number can be at most 20 characters.")]
        public string? Phone { get; set; }

        public string? ProfileImageUrl { get; set; }

        public string Role { get; set; } = "";
    }
}
