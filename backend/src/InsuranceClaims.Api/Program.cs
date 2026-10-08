using InsuranceClaims.Infrastructure;
using InsuranceClaims.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for local development and deployed frontend
var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"];

var allowedOrigins = string.IsNullOrWhiteSpace(configuredOrigins)
    ? new[] { "http://localhost:5173", "http://localhost:3000" }
    : configuredOrigins
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register infrastructure services (DbContext, repositories, auth, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
var swaggerEnabled =
    app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

// CORS
app.UseCors("AllowFrontend");

// Exception handling middleware
app.UseMiddleware<ExceptionMiddleware>();

// Serve uploaded files
app.UseStaticFiles();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InsuranceClaims.Infrastructure.Persistence.ApplicationDbContext>();
    if (db.Database.ProviderName?.Contains("InMemory") == true)
    {
        await db.Database.EnsureCreatedAsync();
        if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.PolicyTypes))
        {
            await InsuranceClaims.Infrastructure.Persistence.Seed.PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(db);
        }
    }
}
catch
{
    // Ignore seed concurrency races during parallel test host instantiation
}

app.Run();

public partial class Program { }
