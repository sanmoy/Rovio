using Matchmaking.MockGameServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SessionStore>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
