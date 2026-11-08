using Microsoft.AspNetCore.Identity;

namespace PhysioClinic.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? RoleTitle { get; set; }
        public int? TherapistId { get; set; }
        public Therapist? Therapist { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
