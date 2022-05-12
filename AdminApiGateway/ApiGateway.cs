using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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

namespace AdminApiGateway
{
    public record ScooterDto(Guid Id, double Latitude, double Longitude, double BatteryLevel, bool Enabled, bool Rented, bool Locked, bool Standby, bool Connected);

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

        [FunctionName("scooters")]
        public async Task<IActionResult> GetScooters(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            var digitalTwinsClient = InstantiateDtClient();

            _logger.LogInformation("Querying twin graph");
            string query = "SELECT * FROM DIGITALTWINS DT WHERE IS_OF_MODEL(DT, 'dtmi:com:escooter:EScooter;1')";
            AsyncPageable<BasicDigitalTwin> result = digitalTwinsClient.QueryAsync<BasicDigitalTwin>(query);
            try
            {
                var scooters = new List<BasicDigitalTwin>();
                await foreach (BasicDigitalTwin twin in result)
                {
                    scooters.Add(twin);
                }

                if (scooters.Count == 0)
                {
                    return new OkObjectResult(scooters);
                }

                _logger.LogInformation("Retrieved scooters");
                _logger.LogInformation("Querying relationships");

                string idString = scooters.Select(x => $"'{x.Id}'").ConcatStrings(", ");

                string queryRents = $"SELECT target FROM DIGITALTWINS source JOIN target RELATED source.is_riding WHERE target.$dtId IN [{idString}]";

                _logger.LogInformation(queryRents);
                var rentResult = digitalTwinsClient.QueryAsync<RentedScooterResultDto>(queryRents);
                var rentedScooters = new List<BasicDigitalTwin>();
                await foreach (RentedScooterResultDto rent in rentResult)
                {
                    rentedScooters.Add(rent.Target);
                }
                _logger.LogInformation(rentedScooters.Select(x => x.Id).ConcatStrings(", "));
                var resultScooters = scooters.GroupJoin(rentedScooters, x => x.Id, y => y.Id, (s, r) => MapTwin(s, r.Any()));
                return new OkObjectResult(resultScooters);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError($"Error {ex.Status}, {ex.ErrorCode}, {ex.Message}");
                throw;
            }
        }

        private static ScooterDto MapTwin(BasicDigitalTwin twin, bool rented)
        {
            Guid id = new Guid(twin.Id);

            var latitude = ((JsonElement)twin.Contents["Latitude"]).GetDouble();
            var longitude = ((JsonElement)twin.Contents["Longitude"]).GetDouble();
            var battery = ((JsonElement)twin.Contents["BatteryLevel"]).GetDouble();
            var enabled = ((JsonElement)twin.Contents["Enabled"]).GetBoolean();
            var locked = ((JsonElement)twin.Contents["Locked"]).GetBoolean();
            var standby = ((JsonElement)twin.Contents["Standby"]).GetBoolean();
            var connected = ((JsonElement)twin.Contents["Connected"]).GetBoolean();

            var scooter = new ScooterDto(id, latitude, longitude, battery, enabled, rented, locked, standby, connected);
            return scooter;
        }
    }
}
