using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdminApiGateway;

public interface IScootersService
{
    Task<IEnumerable<ScooterDto>> GetScooters();
}

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
