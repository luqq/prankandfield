using PrankAndField.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RelayOptions>(builder.Configuration.GetSection("Relay"));
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddSignalR();
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Relay:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();

app.UseCors();
app.UseRelayAccess();
app.MapGet("/health", (RoomRegistry rooms) => Results.Ok(new { status = "ok", rooms = rooms.RoomCount }));
app.MapHub<MatchHub>(MatchHub.Path);

app.Run();

/// <summary>Entry point, public so the integration tests can host the app.</summary>
public partial class Program;
