using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using EasyDesk.Tools.Collections;
using EasyDesk.Tools.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AdminApiGateway;

public record ScooterDto(
    Guid Id,
    double Latitude,
    double Longitude,
    double BatteryLevel,
    bool Enabled,
    bool Rented,
    bool Locked,
    bool Standby,
    bool Connected);

public record CustomerDto(
    Guid Id,
    string Username);

public class RentedScooterResultDto
{
    [JsonPropertyName("target")]
    public BasicDigitalTwin Target { get; set; }
}

public class ApiGateway
{
    private static DigitalTwinsClient InstantiateDtClient()
    {
        string digitalTwinUrl = "https://" + Environment.GetEnvironmentVariable("AzureDTHostname");
        var credential = new DefaultAzureCredential();
        return new DigitalTwinsClient(
            new Uri(digitalTwinUrl),
            credential,
            new DigitalTwinsClientOptions { Transport = new HttpClientTransport(_httpClient) });
    }

    private static readonly HttpClient _httpClient = new HttpClient();

    private readonly ILogger<ApiGateway> _logger;

    public ApiGateway(ILogger<ApiGateway> log)
    {
        _logger = log;
    }

    [FunctionName("customers")]
    public async Task<IActionResult> GetCustomers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        try
        {
            var digitalTwinsClient = InstantiateDtClient();
            var query = "SELECT * FROM DIGITALTWINS DT WHERE IS_OF_MODEL(DT, 'dtmi:com:escooter:Customer;1')";
            var customers = await RunQuery<BasicDigitalTwin, CustomerDto>(digitalTwinsClient, query, ToCustomerDto);
            return new OkObjectResult(customers);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError($"Error {ex.Status}, {ex.ErrorCode}, {ex.Message}");
            throw;
        }
    }

    [FunctionName("scooters")]
    public async Task<IActionResult> GetScooters(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        try
        {
            var digitalTwinsClient = InstantiateDtClient();

            var query = "SELECT * FROM DIGITALTWINS DT WHERE IS_OF_MODEL(DT, 'dtmi:com:escooter:EScooter;1')";
            var scooters = await RunQuery<BasicDigitalTwin, BasicDigitalTwin>(digitalTwinsClient, query, x => x);

            if (scooters.Count == 0)
            {
                return new OkObjectResult(scooters);
            }

            var idString = scooters.Select(x => $"'{x.Id}'").ConcatStrings(", ");
            var queryRents = $"SELECT target FROM DIGITALTWINS source JOIN target RELATED source.is_riding WHERE target.$dtId IN [{idString}]";

            var rentedScooters = await RunQuery<RentedScooterResultDto, BasicDigitalTwin>(digitalTwinsClient, queryRents, x => x.Target);
            var resultScooters = scooters.GroupJoin(rentedScooters, x => x.Id, y => y.Id, (s, r) => ToScooterDto(s, r.Any()));
            return new OkObjectResult(resultScooters);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError($"Error {ex.Status}, {ex.ErrorCode}, {ex.Message}");
            throw;
        }
    }

    private static async Task<List<T>> RunQuery<R, T>(DigitalTwinsClient client, string query, Func<R, T> mapper)
    {
        var result = client.QueryAsync<R>(query);
        var items = new List<T>();
        await foreach (var twin in result)
        {
            items.Add(mapper(twin));
        }
        return items;
    }

    private static ScooterDto ToScooterDto(BasicDigitalTwin twin, bool rented)
    {
        return new ScooterDto(
            Id: Guid.Parse(twin.Id),
            Latitude: twin.ReadProperty("Latitude").GetDouble(),
            Longitude: twin.ReadProperty("Longitude").GetDouble(),
            BatteryLevel: twin.ReadProperty("BatteryLevel").GetDouble(),
            Enabled: twin.ReadProperty("Enabled").GetBoolean(),
            Rented: rented,
            Locked: twin.ReadProperty("Locked").GetBoolean(),
            Standby: twin.ReadProperty("Standby").GetBoolean(),
            Connected: twin.ReadProperty("Connected").GetBoolean());
    }

    private static CustomerDto ToCustomerDto(BasicDigitalTwin twin)
    {
        return new CustomerDto(
            Id: Guid.Parse(twin.Id),
            Username: twin.ReadProperty("Username").GetString());
    }
}
