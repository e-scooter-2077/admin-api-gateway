using AdminApiGateway;
using AdminApiGateway.Services;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;

[assembly: FunctionsStartup(typeof(Startup))]

namespace AdminApiGateway;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var services = builder.Services;
        services.AddHttpClient();
        services.AddSingleton(p =>
        {
            var digitalTwinUrl = "https://" + Environment.GetEnvironmentVariable("AzureDTHostname");
            var credential = new DefaultAzureCredential();
            var httpClient = p.GetRequiredService<HttpClient>();
            return new DigitalTwinsClient(
                new Uri(digitalTwinUrl),
                credential,
                new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
        });

        services.AddSingleton<IScootersService, AdtScootersService>();
        services.AddSingleton<ICustomersService, AdtCustomersService>();
    }
}
