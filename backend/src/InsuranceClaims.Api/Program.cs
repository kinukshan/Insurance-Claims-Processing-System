using InsuranceClaims.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register infrastructure services (DbContext, repositories, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

// TODO: Register application services

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// TODO: Add ExceptionMiddleware
// TODO: Add Authentication & Authorization middleware

app.MapControllers();

app.Run();
