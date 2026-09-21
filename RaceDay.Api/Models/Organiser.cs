namespace RaceDay.Api.Models
{
    public class Organiser
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}