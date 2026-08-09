using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Matchmaking.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHostedService<PartitionMatcherService>();

var host = builder.Build();
host.Run();
