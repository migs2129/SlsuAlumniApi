using System;

using Microsoft.AspNetCore.Mvc;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        public AuthController(IConfiguration config) => _config = config;

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            var username = _config["AdminCredentials:Username"];
            var password = _config["AdminCredentials:Password"];

            if (req.Username == username && req.Password == password)
                return Ok(new { success = true, message = "Login successful" });

            return Unauthorized(new { success = false, message = "Invalid credentials" });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}