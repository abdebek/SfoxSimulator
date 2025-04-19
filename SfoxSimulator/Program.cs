using SfoxSimulator;

var builder = WebApplication.CreateBuilder(args);

// Logging (optional but recommended)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register SignalR
builder.Services.AddSignalR().AddHubOptions<SfoxHub>(options => {
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Register the feed service as a singleton
builder.Services.AddSingleton<ISfoxFeedService, SfoxFeedService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ISfoxFeedService>());

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
app.MapHub<SfoxHub>("/ws");

app.MapOpenApi();

app.UseCors();

app.Run();
