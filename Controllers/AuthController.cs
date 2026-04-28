using AlumniTrackingAPI.Services;
using AlumniTrackingAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;
        public AuthController(AuthService auth) => _auth = auth;

        // PUBLIC — no token needed to log in
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var token = await _auth.LoginAsync(req.Username, req.Password);
            if (token == null)
                return Unauthorized(new { message = "Invalid username or password." });
            return Ok(new { token, message = "Login successful." });
        }

        // PROTECTED
        [HttpGet("me")]
        //[Authorize]
        public IActionResult Me()
        {
            var name = User.FindFirstValue(ClaimTypes.Name);
            var fullName = User.FindFirstValue(ClaimTypes.GivenName);
            var role = User.FindFirstValue(ClaimTypes.Role);
            return Ok(new { username = name, fullName, role });
        }

        [HttpGet("admins")]
        //[Authorize(Roles = "superadmin")]
        public async Task<IActionResult> GetAdmins()
        {
            var admins = await _auth.GetAllAdminsAsync();
            return Ok(admins.Select(a => new
            {
                a.Id,
                a.Username,
                a.FullName,
                a.Role,
                a.IsActive,
                a.CreatedAt
            }));
        }

        [HttpPost("admins")]
        //[Authorize(Roles = "superadmin")]
        public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest req)
        {
            var (ok, msg) = await _auth.CreateAdminAsync(
                req.Username, req.Password, req.FullName, req.Role);
            return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
        }

        [HttpPatch("admins/{id}/toggle")]
        //[Authorize(Roles = "superadmin")]
        public async Task<IActionResult> ToggleAdmin(int id)
        {
            bool ok = await _auth.ToggleActiveAsync(id);
            return ok
                ? Ok(new { message = "Status updated." })
                : BadRequest(new { message = "Cannot modify superadmin." });
        }

        [HttpDelete("admins/{id}")]
        //[Authorize(Roles = "superadmin")]
        public async Task<IActionResult> DeleteAdmin(int id)
        {
            bool ok = await _auth.DeleteAdminAsync(id);
            return ok
                ? Ok(new { message = "Admin deleted." })
                : BadRequest(new { message = "Cannot delete superadmin." });
        }

        [HttpPatch("admins/{id}/password")]
        //[Authorize(Roles = "superadmin")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] PasswordRequest req)
        {
            bool ok = await _auth.ChangePasswordAsync(id, req.NewPassword);
            return ok ? Ok(new { message = "Password changed." }) : NotFound();
        }
    }

    public record LoginRequest(string Username, string Password);
    public record CreateAdminRequest(string Username, string Password, string FullName, string Role);
    public record PasswordRequest(string NewPassword);
}
