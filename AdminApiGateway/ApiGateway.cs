using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AdminApiGateway
{

    public record ScooterDto(Guid Id, double Latitude, double Longitude, double BatteryLevel, bool Enabled, bool Locked, bool Standby);

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

            var scooters = new List<ScooterDto>();

            string query = "SELECT * FROM DIGITALTWINS DT WHERE IS_OF_MODEL(DT, 'dtmi:com:escooter:EScooter;1')";
            AsyncPageable<BasicDigitalTwin> result = digitalTwinsClient.QueryAsync<BasicDigitalTwin>(query);
            try
            {
                await foreach (BasicDigitalTwin twin in result)
                {
                    Guid id = new Guid(twin.Id);

                    var latitude = ((JsonElement)twin.Contents["Latitude"]).GetDouble();
                    var longitude = ((JsonElement)twin.Contents["Longitude"]).GetDouble();
                    var battery = ((JsonElement)twin.Contents["BatteryLevel"]).GetDouble();
                    var enabled = ((JsonElement)twin.Contents["Enabled"]).GetBoolean();
                    var locked = ((JsonElement)twin.Contents["Locked"]).GetBoolean();
                    var standby = ((JsonElement)twin.Contents["Standby"]).GetBoolean();

                    var scooter = new ScooterDto(id, latitude, longitude, battery, enabled, locked, standby);
                    scooters.Add(scooter);
                }
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"Error {ex.Status}, {ex.ErrorCode}, {ex.Message}");
                throw;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(scooters);
            return response;
        }
    }
}
