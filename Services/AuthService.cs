using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlumniTrackingAPI.Data;
using AlumniTrackingAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AlumniTrackingAPI.Services
{
    public class AuthService
    {
        private readonly AlumniDbContext _db;
        private readonly IConfiguration  _config;

        public AuthService(AlumniDbContext db, IConfiguration config)
        {
            _db     = db;
            _config = config;
        }

        // ── Login — returns JWT or null ──────────────────────────────────
        public async Task<string?> LoginAsync(string username, string password)
        {
            var user = await _db.AdminUsers
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

            if (user == null) return null;
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

            return GenerateToken(user);
        }

        // ── Create admin account ─────────────────────────────────────────
        public async Task<(bool success, string message)> CreateAdminAsync(
            string username, string password, string fullName, string role)
        {
            bool exists = await _db.AdminUsers.AnyAsync(u => u.Username == username);
            if (exists) return (false, "Username already exists.");

            var admin = new AdminUser
            {
                Username     = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FullName     = fullName,
                Role         = role,
                CreatedAt    = DateTime.UtcNow,
                IsActive     = true
            };

            _db.AdminUsers.Add(admin);
            await _db.SaveChangesAsync();
            return (true, "Admin account created.");
        }

        // ── Get all admins ───────────────────────────────────────────────
        public async Task<List<AdminUser>> GetAllAdminsAsync()
            => await _db.AdminUsers.OrderBy(u => u.CreatedAt).ToListAsync();

        // ── Toggle active status ─────────────────────────────────────────
        public async Task<bool> ToggleActiveAsync(int id)
        {
            var user = await _db.AdminUsers.FindAsync(id);
            if (user == null || user.Role == "superadmin") return false;
            user.IsActive = !user.IsActive;
            await _db.SaveChangesAsync();
            return true;
        }

        // ── Delete admin ─────────────────────────────────────────────────
        public async Task<bool> DeleteAdminAsync(int id)
        {
            var user = await _db.AdminUsers.FindAsync(id);
            if (user == null || user.Role == "superadmin") return false;
            _db.AdminUsers.Remove(user);
            await _db.SaveChangesAsync();
            return true;
        }

        // ── Change password ──────────────────────────────────────────────
        public async Task<bool> ChangePasswordAsync(int id, string newPassword)
        {
            var user = await _db.AdminUsers.FindAsync(id);
            if (user == null) return false;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _db.SaveChangesAsync();
            return true;
        }

        // ── JWT generator ────────────────────────────────────────────────
        private string GenerateToken(AdminUser user)
        {
            var key   = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.GivenName,      user.FullName),
                new Claim(ClaimTypes.Role,            user.Role)
            };

            var token = new JwtSecurityToken(
                issuer:             _config["Jwt:Issuer"],
                audience:           _config["Jwt:Audience"],
                claims:             claims,
                expires:            DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
