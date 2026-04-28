namespace AlumniTrackingAPI.Models
{
    public class AdminUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;  // BCrypt hash
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "admin";       // "superadmin" | "admin"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
