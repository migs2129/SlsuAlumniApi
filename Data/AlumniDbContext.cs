using Microsoft.EntityFrameworkCore;
using AlumniTrackingAPI.Models;

namespace AlumniTrackingAPI.Data
{
    public class AlumniDbContext : DbContext
    {
        public AlumniDbContext(DbContextOptions<AlumniDbContext> options) : base(options) { }

        public DbSet<AdminUser> AdminUsers { get; set; }
        public DbSet<PendingSubmission> PendingSubmissions { get; set; }
        public DbSet<ExamResult> ExamResults { get; set; }  // ← NEW

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AdminUser>().HasData(new AdminUser
            {
                Id = 1,
                Username = "superadmin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234"),
                FullName = "Super Administrator",
                Role = "superadmin",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
        }
    }
}
