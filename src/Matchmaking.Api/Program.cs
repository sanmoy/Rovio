using Matchmaking.Api.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<StorageClient>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
