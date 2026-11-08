using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Please enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Please tell us your name.")]
        [StringLength(100, ErrorMessage = "Your name can be at most 100 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
        [StringLength(150, ErrorMessage = "Your email can be at most 150 characters.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Please choose a password.")]
        [MinLength(8, ErrorMessage = "Your password needs at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please type your password again.")]
        [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = "";

        [Required(ErrorMessage = "Please choose a role.")]
        [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Please choose a role.")]
        public string Role { get; set; } = "Participant";
    }
}
