using SfoxFeedLibrary;
using SfoxFeedLibrary.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging (optional but recommended)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register SignalR
builder.Services.AddSignalR().AddHubOptions<SfoxSimulatorHub>(options => {
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Register the feed service as a singleton
builder.Services.AddSfoxSimulatorHostedService();

// Configure CORS services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins(["http://localhost:23545", "http://localhost:4200"])
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
});

var app = builder.Build();

// Map your hub
app.MapHub<SfoxSimulatorHub>("/ws");

app.UseCors();

app.Run();
