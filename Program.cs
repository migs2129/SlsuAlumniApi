using System.Text;
using AlumniTrackingAPI.Services;
using AlumniTrackingAPI.BackgroundServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── All data lives in Google Sheets — no SQLite / EF Core needed ──────────
builder.Services.AddSingleton<GoogleSheetsService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<ExamResultService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddHostedService<SyncBackgroundService>();

// ── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

// ── JWT ────────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "";

Console.WriteLine($"[Config] Jwt:Key length={jwtKey.Length} | Issuer='{jwtIssuer}' | Audience='{jwtAudience}'");

if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("Jwt:Key missing");
if (string.IsNullOrWhiteSpace(jwtIssuer)) throw new InvalidOperationException("Jwt:Issuer missing");
if (string.IsNullOrWhiteSpace(jwtAudience)) throw new InvalidOperationException("Jwt:Audience missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        opts.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[JWT REJECTED] {ctx.Exception.GetType().Name}: {ctx.Exception.Message}");
                Console.ResetColor();
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[JWT OK] {ctx.Principal?.Identity?.Name}");
                Console.ResetColor();
                return Task.CompletedTask;
            },
            OnMessageReceived = ctx =>
            {
                var auth = ctx.Request.Headers["Authorization"].FirstOrDefault();
                Console.WriteLine(string.IsNullOrEmpty(auth)
                    ? $"[JWT] No header: {ctx.Request.Method} {ctx.Request.Path}"
                    : $"[JWT] Header OK: {ctx.Request.Method} {ctx.Request.Path}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware order — do not change
app.UseCors("AllowFrontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
