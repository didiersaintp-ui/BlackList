using EMVBlacklist.API.Services;
using EMVBlacklist.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Get filter type from environment or use default
var filterTypeStr = builder.Configuration["FILTER_TYPE"] ?? "CuckooFilter";
var filterType = Enum.Parse<FilterType>(filterTypeStr);

// Register BlacklistService as singleton
var blacklistService = new BlacklistService(filterType);
builder.Services.AddSingleton(blacklistService);

// Initialize in background
var initialCount = int.Parse(builder.Configuration["INITIAL_COUNT"] ?? "1000000");
Task.Run(() => blacklistService.InitializeWithData(initialCount));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

Console.WriteLine($"===========================================");
Console.WriteLine($"EMV Blacklist API - {filterType}");
Console.WriteLine($"Initializing with {initialCount:N0} PANs");
Console.WriteLine($"===========================================");

app.Run();
