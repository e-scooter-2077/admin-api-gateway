using AdminApiGateway.Services;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;

namespace AdminApiGateway;

public class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services =>
            {
                services.AddSingleton(p =>
                {
                    var digitalTwinUrl = "https://" + Environment.GetEnvironmentVariable("AzureDTHostname");
                    var credential = new DefaultAzureCredential();
                    var httpClient = new HttpClient();
                    return new DigitalTwinsClient(
                        new Uri(digitalTwinUrl),
                        credential,
                        new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                });

                services.AddScoped<IScootersService, AdtScootersService>();
                services.AddScoped<ICustomersService, AdtCustomersService>();
            })
            .Build();

        host.Run();
    }
}
