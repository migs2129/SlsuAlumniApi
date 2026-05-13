using AlumniTrackingAPI.Models;
using AlumniTrackingAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AlumniTrackingAPI.Services
{
    public class AuthService
    {
        private readonly GoogleSheetsService _sheets;
        private readonly IConfiguration _config;

        public AuthService(GoogleSheetsService sheets, IConfiguration config)
        {
            _sheets = sheets;
            _config = config;
        }

        // ── Login ─────────────────────────────────────────────────────────
        public async Task<string?> LoginAsync(string username, string password)
        {
            // Seed superadmin on first login attempt if tab is empty
            await SeedSuperadminIfNeededAsync();

            var user = (await _sheets.GetAllAdminsAsync())
                .FirstOrDefault(u =>
                    u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                    u.IsActive);

            if (user == null) return null;
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;

            return GenerateToken(user);
        }

        // ── Create admin ───────────────────────────────────────────────────
        public async Task<(bool success, string message)> CreateAdminAsync(
            string username, string password, string fullName, string role)
        {
            var all = await _sheets.GetAllAdminsAsync();
            bool exists = all.Any(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (exists) return (false, "Username already exists.");

            var admin = new AdminUser
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FullName = fullName,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _sheets.CreateAdminAsync(admin);
            return (true, "Admin account created.");
        }

        // ── Get all admins ─────────────────────────────────────────────────
        public async Task<List<AdminUser>> GetAllAdminsAsync()
            => await _sheets.GetAllAdminsAsync();

        // ── Toggle active ──────────────────────────────────────────────────
        public async Task<bool> ToggleActiveAsync(int id)
        {
            var all = await _sheets.GetAllAdminsAsync();
            var user = all.FirstOrDefault(u => u.Id == id);
            if (user == null || user.Role == "superadmin") return false;
            user.IsActive = !user.IsActive;
            return await _sheets.UpdateAdminAsync(user);
        }

        // ── Delete admin ───────────────────────────────────────────────────
        public async Task<bool> DeleteAdminAsync(int id)
            => await _sheets.DeleteAdminAsync(id);

        // ── Change password ────────────────────────────────────────────────
        public async Task<bool> ChangePasswordAsync(int id, string newPassword)
        {
            var all = await _sheets.GetAllAdminsAsync();
            var user = all.FirstOrDefault(u => u.Id == id);
            if (user == null) return false;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            return await _sheets.UpdateAdminAsync(user);
        }

        // ── Seed superadmin if AdminUsers tab is empty ─────────────────────
        private async Task SeedSuperadminIfNeededAsync()
        {
            var all = await _sheets.GetAllAdminsAsync();
            if (all.Any()) return;

            var superadmin = new AdminUser
            {
                Username = "superadmin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234"),
                FullName = "Super Administrator",
                Role = "superadmin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            await _sheets.CreateAdminAsync(superadmin);
            Console.WriteLine("[Auth] Superadmin seeded to AdminUsers tab.");
        }

        // ── JWT generator ──────────────────────────────────────────────────
        private string GenerateToken(AdminUser user)
        {
            var key = new SymmetricSecurityKey(
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
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
