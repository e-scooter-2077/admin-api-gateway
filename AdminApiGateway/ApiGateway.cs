using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using EasyDesk.Tools.Collections;
using EasyDesk.Tools.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AdminApiGateway
{
    public record ScooterDto(Guid Id, double Latitude, double Longitude, double BatteryLevel, bool Enabled, bool Available, bool Locked, bool Standby, bool Connected);

    public class RentedScooterResultDto
    {
        [JsonPropertyName("target")]
        public BasicDigitalTwin Target { get; set; }
    }

    public static class ApiGateway
    {
        [Function("scooters")]
        public static async Task<HttpResponseData> GetScooters(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("get-scooters");
            string digitalTwinUrl = "https://" + Environment.GetEnvironmentVariable("AzureDTHostname");
            var credential = new DefaultAzureCredential();
            var digitalTwinsClient = new DigitalTwinsClient(new Uri(digitalTwinUrl), credential);

            logger.LogInformation("Querying twin graph");
            string query = "SELECT * FROM DIGITALTWINS DT WHERE IS_OF_MODEL(DT, 'dtmi:com:escooter:EScooter;1')";
            AsyncPageable<BasicDigitalTwin> result = digitalTwinsClient.QueryAsync<BasicDigitalTwin>(query);
            try
            {
                var scooters = new List<BasicDigitalTwin>();
                await foreach (BasicDigitalTwin twin in result)
                {
                    scooters.Add(twin);
                }

                logger.LogInformation("Retrieved scooters");
                logger.LogInformation("Querying relationships");

                string idString = scooters.Select(x => $"'{x.Id}'").ConcatStrings(", ");

                string queryRents = $"SELECT target FROM DIGITALTWINS source JOIN target RELATED source.is_riding WHERE target.$dtId IN [{idString}]";

                logger.LogInformation(queryRents);
                var rentResult = digitalTwinsClient.QueryAsync<RentedScooterResultDto>(queryRents);
                var rentedScooters = new List<BasicDigitalTwin>();
                await foreach (RentedScooterResultDto rent in rentResult)
                {
                    rentedScooters.Add(rent.Target);
                }
                logger.LogInformation(rentedScooters.Select(x => x.Id).ConcatStrings(", "));
                var resultScooters = scooters.GroupJoin(rentedScooters, x => x.Id, y => y.Id, (s, r) => MapTwin(s, !r.Any()));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(resultScooters);
                return response;
            }
            catch (RequestFailedException ex)
            {
                logger.LogError($"Error {ex.Status}, {ex.ErrorCode}, {ex.Message}");
                throw;
            }
        }

        private static ScooterDto MapTwin(BasicDigitalTwin twin, bool available)
        {
            Guid id = new Guid(twin.Id);

            var latitude = ((JsonElement)twin.Contents["Latitude"]).GetDouble();
            var longitude = ((JsonElement)twin.Contents["Longitude"]).GetDouble();
            var battery = ((JsonElement)twin.Contents["BatteryLevel"]).GetDouble();
            var enabled = ((JsonElement)twin.Contents["Enabled"]).GetBoolean();
            var locked = ((JsonElement)twin.Contents["Locked"]).GetBoolean();
            var standby = ((JsonElement)twin.Contents["Standby"]).GetBoolean();
            var connected = ((JsonElement)twin.Contents["Connected"]).GetBoolean();

            var scooter = new ScooterDto(id, latitude, longitude, battery, enabled, available, locked, standby, connected);
            return scooter;
        }
    }
}
