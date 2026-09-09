using Fantastiche.Infrastructure;
using Fantastiche.Scheduler.Jobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddFantasticheInfrastructure(builder.Configuration);
builder.Services.AddHostedService<EmailDispatchJob>();
await builder.Build().RunAsync();
