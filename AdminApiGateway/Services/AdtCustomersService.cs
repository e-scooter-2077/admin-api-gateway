using Azure.DigitalTwins.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdminApiGateway.Services;

public class AdtCustomersService : ICustomersService
{
    private readonly DigitalTwinsClient _client;

    public AdtCustomersService(DigitalTwinsClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<CustomerDto>> GetCustomers()
    {
        var query = "SELECT * FROM DIGITALTWINS DT WHERE IS_OF_MODEL(DT, 'dtmi:com:escooter:Customer;1')";
        var customerTwins = await _client.ListQuery<BasicDigitalTwin>(query);
        return customerTwins.Select(ToCustomerDto);
    }

    private CustomerDto ToCustomerDto(BasicDigitalTwin twin) => new(
        Id: Guid.Parse(twin.Id),
        Username: twin.ReadProperty("Username").GetString());
}
