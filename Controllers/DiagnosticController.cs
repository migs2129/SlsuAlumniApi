using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace AlumniTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticController : ControllerBase
    {
        private readonly IConfiguration _config;
        public DiagnosticController(IConfiguration config) => _config = config;

        // Shows exactly what the running server reads from config
        [HttpGet("config")]
        [AllowAnonymous]
        public IActionResult GetConfig()
        {
            var key = _config["Jwt:Key"] ?? "NULL";
            var issuer = _config["Jwt:Issuer"] ?? "NULL";
            var audience = _config["Jwt:Audience"] ?? "NULL";

            return Ok(new
            {
                keyLength = key.Length,
                keyFull = key,          // ← show full key so we can compare
                issuer,
                audience,
                configPath = AppContext.BaseDirectory  // ← shows which folder app runs from
            });
        }

        // Manually validates the token from the Authorization header
        // and returns exactly WHY it passes or fails
        [HttpGet("validate")]
        [AllowAnonymous]
        public IActionResult ValidateToken()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return BadRequest(new { error = "No Bearer token in Authorization header" });

            var tokenStr = authHeader["Bearer ".Length..].Trim();
            var key = _config["Jwt:Key"] ?? "";
            var issuer = _config["Jwt:Issuer"] ?? "";
            var audience = _config["Jwt:Audience"] ?? "";

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

                handler.ValidateToken(tokenStr, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                }, out var validatedToken);

                var jwt = (JwtSecurityToken)validatedToken;
                return Ok(new
                {
                    result = "VALID",
                    subject = jwt.Subject,
                    issuer = jwt.Issuer,
                    audience = string.Join(",", jwt.Audiences),
                    expires = jwt.ValidTo,
                    keyUsed = key[..Math.Min(8, key.Length)] + "..."
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    result = "INVALID",
                    errorType = ex.GetType().Name,
                    errorMessage = ex.Message,
                    keyUsed = key[..Math.Min(8, key.Length)] + "...",
                    keyLength = key.Length,
                    issuer,
                    audience
                });
            }
        }

        [HttpGet("protected")]
        [Authorize]
        public IActionResult TestProtected()
        {
            var username = User.FindFirstValue(ClaimTypes.Name);
            var role = User.FindFirstValue(ClaimTypes.Role);
            return Ok(new { message = "JWT is valid!", username, role });
        }
    }
}