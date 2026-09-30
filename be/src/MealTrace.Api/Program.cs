using MealTrace.Api.Data;
using MealTrace.Api.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("MealTrace")
    ?? throw new InvalidOperationException("ConnectionStrings:MealTrace is required.");
builder.Services.AddDbContext<MealTraceDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Frontend");
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "MealTrace API" }));
app.MapMealEndpoints();
app.Run();
