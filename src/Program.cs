using Azure.Core;
using Azure.Identity;
using Bicep.Local.Extension.Host.Extensions;
using Bicep.Local.Extension.Types;
using FoundryExtension;
using FoundryExtension.Client;
using FoundryExtension.HostedAgent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);

builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddHttpClient(FoundryClientFactory.HttpClientName);
builder.Services.AddSingleton<IFoundryClientFactory, FoundryClientFactory>();

builder.Services
    .AddBicepExtension()
    .WithDefaults(
        name: "MicrosoftFoundry",
        version: ThisAssembly.AssemblyInformationalVersion.Split('+')[0],
        isSingleton: true)
    .WithConfigurationType<Configuration>()
    .WithTypeAssembly(typeof(Program).Assembly)
    .WithResourceHandler<HostedAgentHandler>();

var app = builder.Build();

app.MapBicepExtension();

await app.RunAsync();
