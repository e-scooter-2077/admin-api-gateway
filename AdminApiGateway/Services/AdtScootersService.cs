using Azure.DigitalTwins.Core;
using EasyDesk.Tools.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AdminApiGateway.Services;

public class RentedScooterResultDto
{
    [JsonPropertyName("target")]
    public BasicDigitalTwin Target { get; set; }
}

public class AdtScootersService : IScootersService
{
    private readonly DigitalTwinsClient _client;

    public AdtScootersService(DigitalTwinsClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ScooterDto>> GetScooters()
    {
        var query = "SELECT * FROM DIGITALTWINS DT WHERE IS_OF_MODEL(DT, 'dtmi:com:escooter:EScooter;1')";
        var scooters = await _client.ListQuery<BasicDigitalTwin>(query);

        if (scooters.IsEmpty())
        {
            return Enumerable.Empty<ScooterDto>();
        }

        var idString = scooters.Select(x => $"'{x.Id}'").ConcatStrings(", ");
        var queryRents = $"SELECT target FROM DIGITALTWINS source JOIN target RELATED source.is_riding WHERE target.$dtId IN [{idString}]";

        var rentedScooters = await _client.ListQuery<RentedScooterResultDto>(queryRents);
        return scooters.GroupJoin(rentedScooters, x => x.Id, y => y.Target.Id, (s, r) => ToScooterDto(s, r.Any()));
    }

    private ScooterDto ToScooterDto(BasicDigitalTwin twin, bool rented) => new(
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
