using Matchmaking.Storage.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TicketStore>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
